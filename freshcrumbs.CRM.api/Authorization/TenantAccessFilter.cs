using System.Security.Claims;
using freshcrumbs.CRM.api.Services;
using freshcrumbs.CRM.domain.entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace freshcrumbs.CRM.api.Authorization
{
    // Global filter: every api/tenant/{companyId}/... request must come from an authenticated user
    // of that same company AND the company must hold an active subscription that includes the
    // feature the endpoint belongs to.
    public class TenantAccessFilter : IAsyncAuthorizationFilter
    {
        private const string SubscriptionSegment = "subscription";
        private const string TermsSegment = "terms";
        private const string SyncSegment = "sync";

        // Tokens issued by api/auth/sync-token carry purpose=sync and may only call api/tenant/{id}/sync/*.
        public const string PurposeClaim = "purpose";
        public const string SyncPurpose = "sync";

        // On tokens issued while the account still uses its temporary password. Only api/auth/change-password accepts them.
        public const string PasswordChangeClaim = "pwd_change";

        // HttpContext.Items key holding the caller's normalized tenant role (null when it is not a valid role).
        public const string RoleItemKey = "TenantRole";
        public const string FeaturesItemKey = "TenantFeatures";

        private readonly IAuthorizationService _authorizationService;
        private readonly ISubscriptionService _subscriptions;
        private readonly ITermsService _terms;

        public TenantAccessFilter(
            IAuthorizationService authorizationService,
            ISubscriptionService subscriptions,
            ITermsService terms)
        {
            _authorizationService = authorizationService;
            _subscriptions = subscriptions;
            _terms = terms;
        }

        public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
        {
            var httpContext = context.HttpContext;

            if (!httpContext.Request.Path.StartsWithSegments("/api/tenant", out var remaining))
            {
                return;
            }

            var parts = (remaining.Value ?? string.Empty).Split('/', StringSplitOptions.RemoveEmptyEntries);

            if (parts.Length == 0 || !int.TryParse(parts[0], out var companyId))
            {
                context.Result = new NotFoundResult();
                return;
            }

            var user = httpContext.User;

            if (user.Identity?.IsAuthenticated != true)
            {
                context.Result = Denied(StatusCodes.Status401Unauthorized, "Unauthenticated", "Please log in to continue.");
                return;
            }

            var tenantAccess = await _authorizationService.AuthorizeAsync(user, null, TenantAccessRequirement.PolicyName);

            if (!tenantAccess.Succeeded)
            {
                context.Result = Denied(StatusCodes.Status403Forbidden, "WrongTenant", "You do not have access to this company.");
                return;
            }

            // A sync-only token (long-lived, issued for background synchronization) can never open any other area,
            // except the read-only subscription snapshot the desktop uses to renew its offline grace period.
            var isSyncArea = parts.Length > 1 && string.Equals(parts[1], SyncSegment, StringComparison.OrdinalIgnoreCase);
            var isSnapshot = parts.Length > 2
                && string.Equals(parts[1], SubscriptionSegment, StringComparison.OrdinalIgnoreCase)
                && string.Equals(parts[2], "snapshot", StringComparison.OrdinalIgnoreCase);

            if (string.Equals(user.FindFirst(PurposeClaim)?.Value, SyncPurpose, StringComparison.Ordinal)
                && !isSyncArea && !isSnapshot)
            {
                context.Result = Denied(StatusCodes.Status403Forbidden, "WrongTokenType", "This token can only be used for synchronization.");
                return;
            }

            // Temporary-password session: only the read-only snapshot the desktop needs to finish signing in.
            if (user.HasClaim(c => c.Type == PasswordChangeClaim) && !isSnapshot)
            {
                context.Result = Denied(StatusCodes.Status403Forbidden, "PasswordChangeRequired", "Set your own password before using FreshCrumbs.");
                return;
            }

            // The account must still exist, be active, and belong to this company (not just hold a valid token).
            var userCheck = await _subscriptions.CheckTenantUserAsync(user.FindFirstValue(ClaimTypes.NameIdentifier), companyId);

            if (userCheck.Error != null)
            {
                context.Result = Denied(StatusCodes.Status403Forbidden, "UserInactive", userCheck.Error);
                return;
            }

            // The role comes from the account in the database, never from the client.
            var role = TenantRoles.Normalize(userCheck.Role);
            httpContext.Items[RoleItemKey] = role;

            var segment = parts.Length > 1 ? parts[1] : null;
            var subSegment = parts.Length > 2 ? parts[2] : null;

            // The tenant must always be able to read its own subscription state (to see why access is denied).
            if (string.Equals(segment, SubscriptionSegment, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            var access = await _subscriptions.CheckAccessAsync(companyId, TenantFeatureMap.RequiredFeature(segment, subSegment));
            httpContext.Items[FeaturesItemKey] = (access.Subscription?.FeatureList ?? new List<string>()).ToList();

            if (!access.Allowed)
            {
                context.Result = Denied(StatusCodes.Status403Forbidden, access.Code, access.Message);
                return;
            }

            // The terms endpoints stay reachable so a pending version can actually be read and accepted.
            if (string.Equals(segment, TermsSegment, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            // A tenant account must hold one of ADMIN / MANAGER / STAFF to use any tenant area.
            if (role == null)
            {
                context.Result = Denied(
                    StatusCodes.Status403Forbidden,
                    "InvalidRole",
                    "This account does not have a valid role (ADMIN, MANAGER or STAFF). Please contact your administrator.");
                return;
            }

            // Action permissions: the subscription must provide the feature AND the role must allow the action.
            // The feature is checked first so a role can never stand in for a missing subscription feature.
            var requiredPermissions = context.ActionDescriptor.EndpointMetadata?
                .OfType<RequireTenantPermissionAttribute>()
                .Select(a => a.Permission)
                ?? Enumerable.Empty<string>();

            foreach (var permission in requiredPermissions)
            {
                if (!TenantPermissions.IsFeatureAvailable(permission, access.Subscription?.FeatureList ?? Array.Empty<string>()))
                {
                    var feature = TenantPermissions.RequiredFeature(permission);

                    context.Result = Denied(
                        StatusCodes.Status403Forbidden,
                        "FeatureNotIncluded",
                        $"Your current plan ({access.Subscription?.PlanName}) does not include {(feature == null ? "this feature" : PlanFeatureKeys.DisplayName(feature))}.");
                    return;
                }

                if (!TenantPermissions.IsRoleAllowed(permission, role))
                {
                    context.Result = Denied(
                        StatusCodes.Status403Forbidden,
                        "RoleNotAllowed",
                        $"Your role ({role}) is not allowed to perform this action.");
                    return;
                }
            }

            var pendingTerms = await _terms.GetPendingForCompanyAsync(companyId);

            if (pendingTerms != null)
            {
                context.Result = Denied(
                    StatusCodes.Status403Forbidden,
                    "TermsNotAccepted",
                    $"Please accept the latest Terms & Conditions (version {pendingTerms.VersionNumber}) to continue.");
            }
        }

        private static ObjectResult Denied(int statusCode, string code, string message)
        {
            return new ObjectResult(new { code, message }) { StatusCode = statusCode };
        }
    }
}
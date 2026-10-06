using System.Security.Claims;
using freshcrumbs.CRM.api.Services;
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

            // The account must still exist, be active, and belong to this company (not just hold a valid token).
            var userError = await _subscriptions.CheckUserAsync(user.FindFirstValue(ClaimTypes.NameIdentifier), companyId);

            if (userError != null)
            {
                context.Result = Denied(StatusCodes.Status403Forbidden, "UserInactive", userError);
                return;
            }

            var segment = parts.Length > 1 ? parts[1] : null;

            // The tenant must always be able to read its own subscription state (to see why access is denied).
            if (string.Equals(segment, SubscriptionSegment, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            var access = await _subscriptions.CheckAccessAsync(companyId, TenantFeatureMap.RequiredFeature(segment));

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
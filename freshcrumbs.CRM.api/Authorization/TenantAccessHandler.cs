using Microsoft.AspNetCore.Authorization;

namespace freshcrumbs.CRM.api.Authorization
{
    public class TenantAccessHandler : AuthorizationHandler<TenantAccessRequirement>
    {
        private readonly IHttpContextAccessor _httpContextAccessor;

        public TenantAccessHandler(IHttpContextAccessor httpContextAccessor)
        {
            _httpContextAccessor = httpContextAccessor;
        }

        protected override Task HandleRequirementAsync(
            AuthorizationHandlerContext context,
            TenantAccessRequirement requirement)
        {
            var routeValue = _httpContextAccessor.HttpContext?.GetRouteValue("companyId")?.ToString();
            var tenantClaim = context.User.FindFirst(TenantAccessRequirement.TenantIdClaim)?.Value;

            if (int.TryParse(routeValue, out var companyId)
                && int.TryParse(tenantClaim, out var tenantId)
                && companyId == tenantId)
            {
                context.Succeed(requirement);
            }

            return Task.CompletedTask;
        }
    }
}
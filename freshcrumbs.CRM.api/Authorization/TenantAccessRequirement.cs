using Microsoft.AspNetCore.Authorization;

namespace freshcrumbs.CRM.api.Authorization
{
    public class TenantAccessRequirement : IAuthorizationRequirement
    {
        public const string PolicyName = "TenantAccess";
        public const string TenantIdClaim = "tenant_id";
    }
}
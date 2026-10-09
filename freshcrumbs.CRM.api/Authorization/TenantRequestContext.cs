using freshcrumbs.CRM.domain.entities;

namespace freshcrumbs.CRM.api.Authorization
{
    public static class TenantRequestContext
    {
        public static string? GetTenantRole(this HttpContext context)
        {
            return context.Items.TryGetValue(TenantAccessFilter.RoleItemKey, out var role) ? role as string : null;
        }

        public static IReadOnlyList<string> GetTenantFeatures(this HttpContext context)
        {
            return context.Items.TryGetValue(TenantAccessFilter.FeaturesItemKey, out var features) && features is List<string> list
                ? list
                : new List<string>();
        }

        public static bool HasTenantPermission(this HttpContext context, string permission)
        {
            return TenantPermissions.IsRoleAllowed(permission, context.GetTenantRole())
                && TenantPermissions.IsFeatureAvailable(permission, context.GetTenantFeatures());
        }
    }
}
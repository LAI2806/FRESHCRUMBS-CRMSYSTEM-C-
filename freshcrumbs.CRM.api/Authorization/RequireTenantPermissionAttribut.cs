namespace freshcrumbs.CRM.api.Authorization
{
    // Marks a tenant endpoint with the action it performs (see TenantPermissions).
    // Read by the global TenantAccessFilter, so there is no second authorization pipeline.
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = true)]
    public sealed class RequireTenantPermissionAttribute : Attribute
    {
        public RequireTenantPermissionAttribute(string permission)
        {
            Permission = permission;
        }

        public string Permission { get; }
    }
}

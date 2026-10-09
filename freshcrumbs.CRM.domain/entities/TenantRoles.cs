namespace freshcrumbs.CRM.domain.entities
{
    // Roles a tenant (company) user can hold. Stored in ApplicationUser.Role.
    // SuperAdmin is a platform role (PlatformRoles) and is intentionally not part of this list.
    public static class TenantRoles
    {
        public const string Admin = "ADMIN";
        public const string Manager = "MANAGER";
        public const string Staff = "STAFF";

        public static readonly IReadOnlyList<string> All = new[] { Admin, Manager, Staff };

        // Returns the canonical upper-case role, or null when the value is not a valid tenant role.
        public static string? Normalize(string? role)
        {
            var value = role?.Trim();

            if (string.IsNullOrEmpty(value))
            {
                return null;
            }

            return All.FirstOrDefault(r => string.Equals(r, value, StringComparison.OrdinalIgnoreCase));
        }

        public static bool IsValid(string? role) => Normalize(role) != null;
    }
}

using freshcrumbs.CRM.winforms.Models;

namespace freshcrumbs.CRM.winforms.Services
{
    // Tenant role names (match the API's TenantRoles).
    public static class TenantRole
    {
        public const string Admin = "ADMIN";
        public const string Manager = "MANAGER";
        public const string Staff = "STAFF";
    }

    public static class AuthSession
    {
        public static LoginResultModel? Current { get; private set; }

        public static string? Token => Current?.Token;

        public static bool IsSuperAdmin => Current?.IsSuperAdmin == true;

        // Tenant role, always upper-case so casing never affects a comparison. Empty for SuperAdmin / no session.
        public static string Role => (Current?.Role ?? string.Empty).Trim().ToUpperInvariant();

        public static bool IsAdmin => IsInRole(TenantRole.Admin);

        public static bool IsManager => IsInRole(TenantRole.Manager);

        public static bool IsStaff => IsInRole(TenantRole.Staff);

        // Plan features and effective permissions reported by the server for the signed-in tenant user.
        public static IReadOnlyList<string> EnabledFeatures { get; private set; } = Array.Empty<string>();

        public static IReadOnlyList<string> Permissions { get; private set; } = Array.Empty<string>();

        public static bool IsInRole(params string[] roles)
        {
            var current = Role;

            return current.Length > 0
                && roles.Any(r => string.Equals(r?.Trim(), current, StringComparison.OrdinalIgnoreCase));
        }

        public static void Start(LoginResultModel login)
        {
            Current = login;
            EnabledFeatures = Array.Empty<string>();
            Permissions = Array.Empty<string>();
        }

        public static void SetAccess(IEnumerable<string>? features, IEnumerable<string>? permissions)
        {
            EnabledFeatures = (features ?? Enumerable.Empty<string>()).ToList();
            Permissions = (permissions ?? Enumerable.Empty<string>()).ToList();
        }

        public static void Clear()
        {
            Current = null;
            EnabledFeatures = Array.Empty<string>();
            Permissions = Array.Empty<string>();
        }
    }
}
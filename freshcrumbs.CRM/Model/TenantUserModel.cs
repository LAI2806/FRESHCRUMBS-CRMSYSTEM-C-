namespace freshcrumbs.CRM.winforms.Models
{
    // An employee account of the signed-in ADMIN's company (Tenant User Management).
    public class TenantUserModel
    {
        public string Id { get; set; } = string.Empty;

        public string UserName { get; set; } = string.Empty;

        public string Email { get; set; } = string.Empty;

        public string FirstName { get; set; } = string.Empty;

        public string LastName { get; set; } = string.Empty;

        public string FullName { get; set; } = string.Empty;

        public string ContactNumber { get; set; } = string.Empty;

        public string Role { get; set; } = string.Empty;

        public string Status { get; set; } = string.Empty;

        public int? BranchId { get; set; }

        public string? BranchName { get; set; }

        public bool IsSelf { get; set; }

        public bool CanEdit { get; set; }

        public string BranchDisplay => BranchName ?? "Unassigned";
    }

    public class TenantUserListModel
    {
        // False when the desktop is offline and the list is the last synced copy (read-only).
        public bool Online { get; set; }

        public List<TenantUserModel> Users { get; set; } = new();
    }

    public class CreatedTenantUserModel
    {
        public TenantUserModel User { get; set; } = new();

        public string TemporaryPassword { get; set; } = string.Empty;
    }
}

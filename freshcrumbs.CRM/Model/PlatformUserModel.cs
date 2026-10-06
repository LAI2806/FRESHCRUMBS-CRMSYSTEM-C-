namespace freshcrumbs.CRM.winforms.Models
{
    public class PlatformUserModel
    {
        public string Id { get; set; } = string.Empty;

        public string UserName { get; set; } = string.Empty;

        public string Email { get; set; } = string.Empty;

        public int? TenantId { get; set; }

        public string FirstName { get; set; } = string.Empty;

        public string LastName { get; set; } = string.Empty;

        public string Role { get; set; } = string.Empty;

        public string Status { get; set; } = string.Empty;

        public string TenantName { get; set; } = string.Empty;

        public string FullName => $"{FirstName} {LastName}".Trim();

        public string UserText => string.IsNullOrWhiteSpace(FullName) ? UserName : $"{FullName} ({UserName})";
    }
}
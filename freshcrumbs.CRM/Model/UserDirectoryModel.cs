namespace freshcrumbs.CRM.winforms.Models
{
    public class UserDirectoryItemModel
    {
        public string UserId { get; set; } = string.Empty;

        public string FullName { get; set; } = string.Empty;

        public string UserName { get; set; } = string.Empty;

        public string Email { get; set; } = string.Empty;

        public int? TenantId { get; set; }

        public string? TenantCode { get; set; }

        public string TenantName { get; set; } = string.Empty;

        public string Role { get; set; } = string.Empty;

        public string Status { get; set; } = string.Empty;

        public string UserText => string.IsNullOrWhiteSpace(FullName) ? UserName : $"{FullName} ({UserName})";
    }
}
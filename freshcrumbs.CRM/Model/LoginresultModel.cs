namespace freshcrumbs.CRM.winforms.Models
{
    public class LoginResultModel
    {
        public string Token { get; set; } = string.Empty;

        public DateTime? ExpiresAt { get; set; }

        public string UserId { get; set; } = string.Empty;

        public string UserName { get; set; } = string.Empty;

        public string FullName { get; set; } = string.Empty;

        public string Email { get; set; } = string.Empty;

        public List<string> Roles { get; set; } = new();

        public bool IsSuperAdmin { get; set; }

        public int? TenantId { get; set; }

        public string? CompanyCode { get; set; }

        public string? CompanyName { get; set; }
    }
}
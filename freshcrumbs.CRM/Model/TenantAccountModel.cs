namespace freshcrumbs.CRM.winforms.Models
{
    // My Account of the signed-in MainCRM user. Role and company are read-only.
    public class TenantAccountModel
    {
        public string FirstName { get; set; } = string.Empty;

        public string LastName { get; set; } = string.Empty;

        public string FullName { get; set; } = string.Empty;

        public string UserName { get; set; } = string.Empty;

        public string Email { get; set; } = string.Empty;

        public string ContactNumber { get; set; } = string.Empty;

        public string? Role { get; set; }

        public string CompanyName { get; set; } = string.Empty;

        // False when the desktop is offline: the account is shown read-only.
        public bool Online { get; set; }
    }
}

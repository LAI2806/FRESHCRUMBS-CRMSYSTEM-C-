using Microsoft.AspNetCore.Identity;

namespace freshcrumbs.CRM.domain.entities
{
    public class ApplicationUser : IdentityUser
    {
        public int TenantId { get; set; }

        public string FirstName { get; set; } = string.Empty;

        public string LastName { get; set; } = string.Empty;

        public string ContactNumber { get; set; } = string.Empty;

        public string Role { get; set; } = string.Empty;

        public string Status { get; set; } = "Active";

        public Company? Company { get; set; }
    }
}
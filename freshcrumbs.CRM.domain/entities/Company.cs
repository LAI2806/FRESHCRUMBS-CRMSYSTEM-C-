using System;
using System.Collections.Generic;
using System.Text;

namespace freshcrumbs.CRM.domain.entities
{
    public class Company
    {
        public int CompanyId { get; set; }

        public string CompanyCode { get; set; } = string.Empty;

        public string CompanyName { get; set; } = string.Empty;

        public string BusinessAddress { get; set; } = string.Empty;

        public string ContactNo { get; set; } = string.Empty;

        public string Email { get; set; } = string.Empty;

        public string Status { get; set; } = "Active";

        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public ICollection<ApplicationUser> Users { get; set; } = new List<ApplicationUser>();
    }
}
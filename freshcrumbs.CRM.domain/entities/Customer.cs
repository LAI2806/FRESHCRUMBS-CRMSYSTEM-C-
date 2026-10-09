using System;
using System.Collections.Generic;

namespace freshcrumbs.CRM.domain.entities
{
    public class Customer : ISyncEntity
    {
        // Sync identity (offline-first): global id used to match records between local and cloud databases.
        public Guid RowGuid { get; set; } = Guid.NewGuid();

        // UTC time of the last change; used for conflict resolution and incremental pull.
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        public int CustomerId { get; set; }

        public string CustomerCode { get; set; } = string.Empty;

        public string FirstName { get; set; } = string.Empty;

        public string LastName { get; set; } = string.Empty;

        public string Email { get; set; } = string.Empty;

        public string ContactNo { get; set; } = string.Empty;

        public string Address { get; set; } = string.Empty;

        public int LoyaltyPoints { get; set; } = 0;

        public string Status { get; set; } = "Active";

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // PREMIUM (Branching) only: the branch where the customer was registered. NOT ownership: a customer is one
        // shared company record, also associated with every branch where they have sales. Null for older customers.
        public int? BranchId { get; set; }

        // Not stored. Names of the branches this customer is associated with (registration + sales), for display.
        public string? BranchNames { get; set; }

        public ICollection<CustomerDiscountEligibility> DiscountEligibilities { get; set; } = new List<CustomerDiscountEligibility>();
    }
}
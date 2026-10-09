using System;

namespace freshcrumbs.CRM.domain.entities
{
    public class Promotion : ISyncEntity
    {
        // Sync identity (offline-first): global id used to match records between local and cloud databases.
        public Guid RowGuid { get; set; } = Guid.NewGuid();

        // UTC time of the last change; used for conflict resolution and incremental pull.
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        public int PromotionId { get; set; }

        public string PromotionName { get; set; } = string.Empty;

        public string Description { get; set; } = string.Empty;

        public string DiscountType { get; set; } = string.Empty;

        public decimal DiscountValue { get; set; }

        public decimal MinimumPurchase { get; set; }

        public int RequiredLoyaltyPoints { get; set; }

        public DateTime StartDate { get; set; }

        public DateTime EndDate { get; set; }

        public string Status { get; set; } = "Active";

        // When set (e.g. "Senior Citizen"), only customers with a Verified eligibility
        // record for this category may use this promotion. Null/empty = open to everyone.
        public string? EligibilityCategory { get; set; }

        // PREMIUM: null = company-wide promotion; a value = only valid at that branch.
        public int? BranchId { get; set; }
    }
}
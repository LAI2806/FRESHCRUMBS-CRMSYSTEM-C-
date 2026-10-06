using System;

namespace freshcrumbs.CRM.domain.entities
{
    public class Promotion
    {
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
    }
}
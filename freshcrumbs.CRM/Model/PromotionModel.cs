namespace freshcrumbs.CRM.winforms.Models
{
    public class PromotionModel
    {
        public int PromotionId { get; set; }

        public string PromotionName { get; set; } = string.Empty;

        public string Description { get; set; } = string.Empty;

        public string DiscountType { get; set; } = string.Empty;

        public decimal DiscountValue { get; set; }

        public int RequiredLoyaltyPoints { get; set; }

        public decimal MinimumPurchase { get; set; }

        public DateTime StartDate { get; set; } = DateTime.Today;

        public DateTime EndDate { get; set; } = DateTime.Today.AddDays(7);

        public string Status { get; set; } = string.Empty;

        public string? EligibilityCategory { get; set; }
    }
}
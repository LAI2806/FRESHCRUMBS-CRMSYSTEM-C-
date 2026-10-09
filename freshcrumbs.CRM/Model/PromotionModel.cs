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

        // PREMIUM: null = company-wide; a value = only valid at that branch.
        public int? BranchId { get; set; }

        public string? BranchName { get; set; }

        // Decided by the server for the signed-in user (PREMIUM MANAGER: only their own branch's promotions).
        public bool CanManage { get; set; } = true;
    }
}
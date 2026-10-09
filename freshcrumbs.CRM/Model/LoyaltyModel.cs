namespace freshcrumbs.CRM.winforms.Models
{
    public class LoyaltyModel
    {
        public int LoyaltyTransactionId { get; set; }

        public int CustomerId { get; set; }

        public int PointsEarned { get; set; }

        public int PointsUsed { get; set; }

        public string TransactionType { get; set; } = string.Empty;

        public DateTime Date { get; set; } = DateTime.Now;

        // Branch of the sale that created the row (null for manual adjustments or non-branching plans).
        public string? BranchName { get; set; }

        // Decided by the server for the signed-in user (PREMIUM MANAGER: only their branch's sale-based rows).
        public bool CanModify { get; set; } = true;
    }
}
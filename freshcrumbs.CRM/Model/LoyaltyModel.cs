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
    }
}
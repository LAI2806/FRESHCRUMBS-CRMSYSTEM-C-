namespace freshcrumbs.CRM.winforms.Models
{
    public class SalesTransactionModel
    {
        public int TransactionId { get; set; }

        public int CustomerId { get; set; }

        public int? PromotionId { get; set; }

        public DateTime TransactionDate { get; set; } = DateTime.Now;

        public decimal TotalAmount { get; set; }

        public decimal DiscountAmount { get; set; }

        public int PointsUsed { get; set; }

        public int PointsEarned { get; set; }

        public decimal FinalAmount { get; set; }

        public string PaymentMethod { get; set; } = string.Empty;

        public string Status { get; set; } = "Completed";
    }
}
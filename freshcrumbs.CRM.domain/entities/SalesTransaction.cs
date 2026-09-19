using System;
using System.Collections.Generic;

namespace freshcrumbs.CRM.domain.entities
{
    public class SalesTransaction
    {
        public int TransactionId { get; set; }

        public int CustomerId { get; set; }

        public int? PromotionId { get; set; }

        public DateTime TransactionDate { get; set; } = DateTime.UtcNow;

        public decimal TotalAmount { get; set; }

        public decimal DiscountAmount { get; set; }

        public int PointsUsed { get; set; }

        public int PointsEarned { get; set; }

        public decimal FinalAmount { get; set; }

        public string PaymentMethod { get; set; } = string.Empty;

        public string Status { get; set; } = "Completed";

        public Customer? Customer { get; set; }

        public Promotion? Promotion { get; set; }

        public ICollection<TransactionItem> TransactionItems { get; set; } = new List<TransactionItem>();
    }
}
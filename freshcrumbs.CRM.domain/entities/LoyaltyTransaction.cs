using System;

namespace freshcrumbs.CRM.domain.entities
{
    public class LoyaltyTransaction
    {
        public int LoyaltyTransactionId { get; set; }

        public int CustomerId { get; set; }

        public int? SalesTransactionId { get; set; }

        public int PointsEarned { get; set; }

        public int PointsUsed { get; set; }

        public string TransactionType { get; set; } = string.Empty;

        public DateTime Date { get; set; } = DateTime.UtcNow;

        public Customer? Customer { get; set; }

        public SalesTransaction? SalesTransaction { get; set; }
    }
}
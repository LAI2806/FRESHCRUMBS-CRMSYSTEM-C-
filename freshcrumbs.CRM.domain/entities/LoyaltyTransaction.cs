using System;

namespace freshcrumbs.CRM.domain.entities
{
    public class LoyaltyTransaction : ISyncEntity
    {
        // Sync identity (offline-first): global id used to match records between local and cloud databases.
        public Guid RowGuid { get; set; } = Guid.NewGuid();

        // UTC time of the last change; used for conflict resolution and incremental pull.
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        public int LoyaltyTransactionId { get; set; }

        public int CustomerId { get; set; }

        public int? SalesTransactionId { get; set; }

        public int PointsEarned { get; set; }

        public int PointsUsed { get; set; }

        public string TransactionType { get; set; } = string.Empty;

        public DateTime Date { get; set; } = DateTime.UtcNow;

        public Customer? Customer { get; set; }

        public bool IsDeleted { get; set; } = false;

        public SalesTransaction? SalesTransaction { get; set; }
    }
}
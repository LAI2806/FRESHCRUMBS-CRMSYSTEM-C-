using System;
using System.Collections.Generic;

namespace freshcrumbs.CRM.domain.entities
{
    public class SalesTransaction : ISyncEntity
    {
        // Sync identity (offline-first): global id used to match records between local and cloud databases.
        public Guid RowGuid { get; set; } = Guid.NewGuid();

        // UTC time of the last change; used for conflict resolution and incremental pull.
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        public int TransactionId { get; set; }

        public int CustomerId { get; set; }

        public int? PromotionId { get; set; }

        // PREMIUM (Branching) sales only. Null for non-branch companies and for historical sales.
        public int? BranchId { get; set; }

        public DateTime TransactionDate { get; set; } = DateTime.UtcNow;

        public decimal TotalAmount { get; set; }

        public decimal DiscountAmount { get; set; }

        public decimal CustomerDiscountAmount { get; set; }

        public int PointsUsed { get; set; }

        public int PointsEarned { get; set; }

        public decimal FinalAmount { get; set; }

        public string PaymentMethod { get; set; } = string.Empty;

        public string Status { get; set; } = "Completed";

        public Customer? Customer { get; set; }

        public Promotion? Promotion { get; set; }
        public bool IsDeleted { get; set; } = false;

        public ICollection<TransactionItem> TransactionItems { get; set; } = new List<TransactionItem>();
    }
}
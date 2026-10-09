using System;
using System.Collections.Generic;

namespace freshcrumbs.CRM.domain.entities
{
    public class Product : ISyncEntity
    {
        // Sync identity (offline-first): global id used to match records between local and cloud databases.
        public Guid RowGuid { get; set; } = Guid.NewGuid();

        // UTC time of the last change; used for conflict resolution and incremental pull.
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        public int ProductId { get; set; }

        public string ProductCode { get; set; } = string.Empty;

        public string ProductName { get; set; } = string.Empty;

        public string Category { get; set; } = string.Empty;

        public string Description { get; set; } = string.Empty;

        public decimal Price { get; set; }

        public int Quantity { get; set; }

        public int ReorderLevel { get; set; } = 10;

        public int Sold { get; set; }

        // Not stored. Stock of this product at the caller's assigned branch (PREMIUM only).
        public int? BranchQuantity { get; set; }

        public string StockLevel =>
            Quantity <= 0 ? "Out of Stock" :
            Quantity <= ReorderLevel ? "Low Stock" :
            "In Stock";

        public string Status { get; set; } = "Active";

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public ICollection<TransactionItem> TransactionItems { get; set; } = new List<TransactionItem>();
    }
}
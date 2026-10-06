using System;
using System.Collections.Generic;

namespace freshcrumbs.CRM.domain.entities
{
    public class Product
    {
        public int ProductId { get; set; }

        public string ProductCode { get; set; } = string.Empty;

        public string ProductName { get; set; } = string.Empty;

        public string Category { get; set; } = string.Empty;

        public string Description { get; set; } = string.Empty;

        public decimal Price { get; set; }

        public int Quantity { get; set; }

        public int ReorderLevel { get; set; } = 10;

        public int Sold { get; set; }

        public string StockLevel =>
            Quantity <= 0 ? "Out of Stock" :
            Quantity <= ReorderLevel ? "Low Stock" :
            "In Stock";

        public string Status { get; set; } = "Active";

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public ICollection<TransactionItem> TransactionItems { get; set; } = new List<TransactionItem>();
    }
}
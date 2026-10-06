namespace freshcrumbs.CRM.winforms.Models
{
    public class ProductModel
    {
        public int ProductId { get; set; }

        public string ProductCode { get; set; } = string.Empty;

        public string ProductName { get; set; } = string.Empty;

        public string Category { get; set; } = string.Empty;

        public string Description { get; set; } = string.Empty;

        public decimal Price { get; set; }

        public int Quantity { get; set; }

        public int Sold { get; set; }

        public int ReorderLevel { get; set; } = 10;

        public string StockLevel { get; set; } = string.Empty;

        public string Status { get; set; } = string.Empty;
    }
}
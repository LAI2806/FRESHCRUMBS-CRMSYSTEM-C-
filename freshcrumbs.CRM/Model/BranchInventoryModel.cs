namespace freshcrumbs.CRM.winforms.Models
{
    public class BranchInventoryModel
    {
        public int ProductId { get; set; }

        public string ProductCode { get; set; } = string.Empty;

        public string ProductName { get; set; } = string.Empty;

        public string Category { get; set; } = string.Empty;

        public decimal Price { get; set; }

        public int Quantity { get; set; }

        public int ReorderLevel { get; set; }

        public string StockLevel { get; set; } = string.Empty;
    }
}

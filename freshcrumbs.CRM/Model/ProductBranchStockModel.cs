namespace freshcrumbs.CRM.winforms.Models
{
    public class ProductBranchStockModel
    {
        public int ProductId { get; set; }

        public string ProductCode { get; set; } = string.Empty;

        public string ProductName { get; set; } = string.Empty;

        public int ReorderLevel { get; set; }

        public int TotalQuantity { get; set; }

        public int AllocatedQuantity { get; set; }

        public int UnallocatedQuantity { get; set; }

        public List<ProductBranchStockLineModel> Branches { get; set; } = new();
    }

    public class ProductBranchStockLineModel
    {
        public int BranchId { get; set; }

        public string BranchName { get; set; } = string.Empty;

        public string Status { get; set; } = string.Empty;

        public int Quantity { get; set; }
    }
}

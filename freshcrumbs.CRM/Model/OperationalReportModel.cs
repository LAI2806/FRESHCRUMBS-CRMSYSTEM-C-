namespace freshcrumbs.CRM.winforms.Models
{
    // GET reports/operations: read-only operational figures. A section is null when the user's role or plan
    // does not include that module.
    public class OperationalReportModel
    {
        public string Period { get; set; } = string.Empty;
        public bool BranchScoped { get; set; }
        public string? BranchName { get; set; }
        public OperationalSalesModel Sales { get; set; } = new();
        public OperationalCustomersModel Customers { get; set; } = new();
        public OperationalProductsModel? Products { get; set; }
        public OperationalFeedbackModel? Feedback { get; set; }
        public OperationalInquiriesModel? Inquiries { get; set; }
        public OperationalPromotionsModel? Promotions { get; set; }
        public OperationalLoyaltyModel? Loyalty { get; set; }
    }

    public class OperationalSalesModel
    {
        public decimal TotalSales { get; set; }
        public int Transactions { get; set; }
        public decimal AverageSale { get; set; }
        public List<ChartPointModel> Trend { get; set; } = new();
        public List<OperationalSaleRowModel> Recent { get; set; } = new();
    }

    public class OperationalSaleRowModel
    {
        public DateTime Date { get; set; }
        public string Customer { get; set; } = string.Empty;
        public decimal FinalAmount { get; set; }
        public string PaymentMethod { get; set; } = string.Empty;
    }

    public class OperationalCustomersModel
    {
        public int ActiveCustomers { get; set; }
        public int NewCustomers { get; set; }
        public List<OperationalCustomerRowModel> Recent { get; set; } = new();
    }

    public class OperationalCustomerRowModel
    {
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public DateTime LastPurchase { get; set; }
        public int Purchases { get; set; }
    }

    public class OperationalProductsModel
    {
        public int ActiveProducts { get; set; }
        public int LowStockCount { get; set; }
        public List<OperationalStockRowModel> LowStock { get; set; } = new();
        public List<ChartPointModel> TopProducts { get; set; } = new();
    }

    public class OperationalStockRowModel
    {
        public string Name { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public int ReorderLevel { get; set; }
    }

    public class OperationalFeedbackModel
    {
        public int Total { get; set; }
        public int Complaints { get; set; }
        public int Open { get; set; }
        public List<ChartPointModel> ByCategory { get; set; } = new();
        public List<OperationalFeedbackRowModel> Recent { get; set; } = new();
    }

    public class OperationalFeedbackRowModel
    {
        public DateTime Date { get; set; }
        public string Customer { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
    }

    public class OperationalInquiriesModel
    {
        public int Total { get; set; }
        public int Responded { get; set; }
        public int Pending { get; set; }
        public List<ChartPointModel> ByStatus { get; set; } = new();
        public List<OperationalInquiryRowModel> Recent { get; set; } = new();
    }

    public class OperationalInquiryRowModel
    {
        public DateTime Date { get; set; }
        public string Customer { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public string Subject { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
    }

    public class OperationalPromotionsModel
    {
        public int ActivePromotions { get; set; }
        public int SalesWithPromotion { get; set; }
        public List<ChartPointModel> Usage { get; set; } = new();
    }

    public class OperationalLoyaltyModel
    {
        public int PointsEarned { get; set; }
        public int PointsUsed { get; set; }
    }
}

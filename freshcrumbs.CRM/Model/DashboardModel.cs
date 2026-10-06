namespace freshcrumbs.CRM.winforms.Models
{
    public class DashboardModel
    {
        public int ActiveCustomers { get; set; }
        public int ActiveProducts { get; set; }
        public decimal TodaysSales { get; set; }
        public int LowStockCount { get; set; }
        public List<RecentSaleModel> RecentSales { get; set; } = new();
        public List<LowStockProductModel> LowStockProducts { get; set; } = new();
        public List<ChartPointModel> SalesOverview { get; set; } = new();
        public int NewCustomersThisMonth { get; set; }
        public int ActiveCustomersThisMonth { get; set; }
        public int LoyaltyActivityThisMonth { get; set; }
    }

    public class RecentSaleModel
    {
        public string Customer { get; set; } = string.Empty;
        public decimal FinalAmount { get; set; }
        public string PaymentMethod { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
    }

    public class LowStockProductModel
    {
        public string ProductName { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public int ReorderLevel { get; set; }
    }
}
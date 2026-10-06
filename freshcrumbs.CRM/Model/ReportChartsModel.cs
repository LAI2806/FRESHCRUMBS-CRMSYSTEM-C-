namespace freshcrumbs.CRM.winforms.Models
{
    public class ChartPointModel
    {
        public string Label { get; set; } = string.Empty;
        public DateTime? Date { get; set; }
        public double Value { get; set; }
    }

    public class ReportChartsModel
    {
        public List<ChartPointModel> SalesTrend { get; set; } = new();
        public List<ChartPointModel> ProductPerformance { get; set; } = new();
        public List<ChartPointModel> CustomerRetention { get; set; } = new();
        public List<ChartPointModel> CustomerStatus { get; set; } = new();
        public List<ChartPointModel> PromotionUsage { get; set; } = new();
        public List<ChartPointModel> LoyaltyPoints { get; set; } = new();
        public List<ChartPointModel> FeedbackCategories { get; set; } = new();
        public List<ChartPointModel> InquiryTypes { get; set; } = new();
        public List<ChartPointModel> CustomerVisitTime { get; set; } = new();

        public List<ChartPointModel> CustomerGrowth { get; set; } = new();
        public List<ChartPointModel> NewVsReturningCustomers { get; set; } = new();
        public List<ChartPointModel> TopLoyalCustomers { get; set; } = new();
        public List<ChartPointModel> SalesByCategory { get; set; } = new();
        public List<ChartPointModel> ProductStock { get; set; } = new();
        public List<ChartPointModel> SalesByPromotion { get; set; } = new();
        public List<ChartPointModel> DiscountGiven { get; set; } = new();
        public List<ChartPointModel> PointsByCustomer { get; set; } = new();
        public List<ChartPointModel> DiscountByPromotion { get; set; } = new();
        public List<ChartPointModel> PromotionStatus { get; set; } = new();
        public List<ChartPointModel> ComplaintsByCategory { get; set; } = new();
        public List<ChartPointModel> FeedbackByCategory { get; set; } = new();
        public List<ChartPointModel> FeedbackTrend { get; set; } = new();
        public List<ChartPointModel> InquiryTrend { get; set; } = new();
    }
}
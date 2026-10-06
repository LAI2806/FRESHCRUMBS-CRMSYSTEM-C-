namespace freshcrumbs.CRM.winforms.Models
{
    public class ReportsOverviewModel
    {
        public decimal TotalSales { get; set; }
        public decimal PreviousPeriodSales { get; set; }
        public int TransactionCount { get; set; }
        public decimal AverageTransaction { get; set; }
        public int TotalProductsSold { get; set; }
        public int TotalCustomers { get; set; }
        public int NewCustomersLast30Days { get; set; }
        public double RepeatCustomerRatePercent { get; set; }
        public int TotalActivePromotions { get; set; }
        public int TotalEarnedPoints { get; set; }
        public int TotalUsedPoints { get; set; }
        public int TotalResolvedFeedback { get; set; }
        public int TotalResolvedComplaints { get; set; }
        public int TotalRespondedInquiries { get; set; }
        public double ResolutionRatePercent { get; set; }
        public double ResponseRatePercent { get; set; }
    }
}
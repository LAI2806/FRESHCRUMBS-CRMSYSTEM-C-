namespace freshcrumbs.CRM.winforms.Models
{
    // PREMIUM Branch BI. Scope: "Company" (ADMIN), "Branch" (MANAGER) or "NotAssigned".
    public class BranchDashboardModel
    {
        public string Scope { get; set; } = string.Empty;

        public string Period { get; set; } = string.Empty;

        public int? ActiveBranches { get; set; }

        public int? MaxBranches { get; set; }

        public List<BranchDashboardRowModel> Branches { get; set; } = new();
    }

    public class BranchDashboardRowModel
    {
        public int? BranchId { get; set; }

        public string BranchName { get; set; } = string.Empty;

        public string Status { get; set; } = string.Empty;

        // Sales recorded before branching (no branch). Never attributed to a branch.
        public bool IsHistorical { get; set; }

        public decimal Revenue { get; set; }

        public int SalesCount { get; set; }

        public decimal AverageSale { get; set; }

        public int? PurchasingCustomers { get; set; }

        public int? ReturningCustomers { get; set; }

        public int? NewCustomers { get; set; }

        public double? RetentionRatePercent { get; set; }

        public decimal TodayRevenue { get; set; }

        public int TodaySalesCount { get; set; }

        public DateTime? LastSaleAt { get; set; }

        public int? UnitsOnHand { get; set; }

        public int? LowStockItems { get; set; }

        public int? AssignedAccounts { get; set; }
    }
}

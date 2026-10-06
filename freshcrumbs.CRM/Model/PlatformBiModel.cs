namespace freshcrumbs.CRM.winforms.Models
{
    public class PlatformBiModel
    {
        public int Days { get; set; }

        public int TotalCompanies { get; set; }

        public int TotalSubscribers { get; set; }

        public int NewSubscribers { get; set; }

        public decimal MonthlyRecurringValue { get; set; }

        public List<MonthlyPointItem> Months { get; set; } = new();

        public List<PlanDistributionItem> PlanPopularity { get; set; } = new();

        public int ActiveCompanies { get; set; }

        public SubscriptionStatusCounts SubscriptionStatus { get; set; } = new();

        public int ActivePlans { get; set; }

        public int InactivePlans { get; set; }

        public List<PlanDistributionItem> PlanDistribution { get; set; } = new();

        public List<ChangeTypeCount> ChangesByType { get; set; } = new();

        public List<RecentChangeItem> RecentChanges { get; set; } = new();

        public List<UserUsageItem> UserUsage { get; set; } = new();

        public List<ExpiringItem> ExpiringSoon { get; set; } = new();

        public TermsSummaryItem? Terms { get; set; }
    }

    public class SubscriptionStatusCounts
    {
        public int Active { get; set; }

        public int Expired { get; set; }

        public int Cancelled { get; set; }

        public int Suspended { get; set; }

        public int Scheduled { get; set; }

        public int NoSubscription { get; set; }
    }

    public class PlanDistributionItem
    {
        public string PlanCode { get; set; } = string.Empty;

        public string PlanName { get; set; } = string.Empty;

        public int Count { get; set; }
    }

    public class ChangeTypeCount
    {
        public string ChangeType { get; set; } = string.Empty;

        public int Count { get; set; }
    }

    public class RecentChangeItem
    {
        public string CompanyCode { get; set; } = string.Empty;

        public string CompanyName { get; set; } = string.Empty;

        public string ChangeType { get; set; } = string.Empty;

        public string PlanName { get; set; } = string.Empty;

        public DateTime EffectiveDate { get; set; }

        public string ChangedBy { get; set; } = string.Empty;

        public string? Reason { get; set; }

        public string Company => $"{CompanyName} ({CompanyCode})";

        public string EffectiveText => EffectiveDate.ToString("yyyy-MM-dd");
    }

    public class UserUsageItem
    {
        public int CompanyId { get; set; }

        public string CompanyCode { get; set; } = string.Empty;

        public string CompanyName { get; set; } = string.Empty;

        public string PlanName { get; set; } = string.Empty;

        public int ActiveUsers { get; set; }

        public int MaxUsers { get; set; }

        public int PercentUsed { get; set; }

        public string Company => $"{CompanyName} ({CompanyCode})";

        public string UsageText => $"{ActiveUsers} / {MaxUsers} Users";

        public string PercentText => $"{PercentUsed}%";
    }

    public class ExpiringItem
    {
        public string CompanyCode { get; set; } = string.Empty;

        public string CompanyName { get; set; } = string.Empty;

        public string PlanName { get; set; } = string.Empty;

        public DateTime EndDate { get; set; }

        public int DaysLeft { get; set; }

        public string Company => $"{CompanyName} ({CompanyCode})";

        public string EndText => EndDate.ToString("yyyy-MM-dd");

        public string DaysLeftText => DaysLeft <= 1 ? "1 day" : $"{DaysLeft} days";
    }

    public class TermsSummaryItem
    {
        public int VersionNumber { get; set; }

        public string Title { get; set; } = string.Empty;

        public bool RequiresAcceptance { get; set; }

        public int AcceptedCompanies { get; set; }

        public int PendingCompanies { get; set; }
    }

    public class MonthlyPointItem
    {
        public string Month { get; set; } = string.Empty;

        public int NewTenants { get; set; }

        public int TotalTenants { get; set; }

        public int ActiveSubscriptions { get; set; }

        public int InactiveSubscriptions { get; set; }

        public string ShortMonth => Month.Length >= 8 ? Month.Substring(0, 3) + " '" + Month.Substring(Month.Length - 2) : Month;
    }
}
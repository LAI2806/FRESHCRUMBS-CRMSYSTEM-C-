namespace freshcrumbs.CRM.domain.entities
{
    public class Subscription
    {
        public int SubscriptionId { get; set; }

        public int CompanyId { get; set; }

        public Company? Company { get; set; }

        public int PlanId { get; set; }

        public Plan? Plan { get; set; }

        // Snapshot of the plan terms at the time this subscription record was created.
        public string PlanCode { get; set; } = string.Empty;

        public string PlanName { get; set; } = string.Empty;

        public decimal Price { get; set; }

        public BillingCycle BillingCycle { get; set; } = BillingCycle.Monthly;

        // Comma-separated feature keys (includes "Branching" when branching is enabled).
        public string Features { get; set; } = string.Empty;

        public int MaxUsers { get; set; }

        public bool BranchingEnabled { get; set; }

        public int? MaxBranches { get; set; }

        public DateTime StartDate { get; set; }

        // Exclusive: the subscription is in force while now < EndDate.
        public DateTime EndDate { get; set; }

        // Stored lifecycle status. "Expired" is never stored; it is derived from the dates.
        public SubscriptionStatus Status { get; set; } = SubscriptionStatus.Active;

        public SubscriptionChangeType ChangeType { get; set; } = SubscriptionChangeType.New;

        public string? Reason { get; set; }

        public string ChangedBy { get; set; } = string.Empty;

        public int? PreviousSubscriptionId { get; set; }

        public Subscription? PreviousSubscription { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public IReadOnlyList<string> FeatureList =>
            Features.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        public bool HasFeature(string featureKey) =>
            FeatureList.Contains(featureKey, StringComparer.OrdinalIgnoreCase);

        public static string JoinFeatures(IEnumerable<string> featureKeys)
        {
            var keys = featureKeys.ToList();
            return string.Join(",", PlanFeatureKeys.All.Where(k => keys.Contains(k)));
        }

        public static DateTime AddCycle(DateTime start, BillingCycle cycle) => cycle switch
        {
            BillingCycle.Weekly => start.AddDays(7),
            BillingCycle.Yearly => start.AddYears(1),
            _ => start.AddMonths(1)
        };

        public bool IsCurrent(DateTime utcNow) =>
            (Status == SubscriptionStatus.Active || Status == SubscriptionStatus.Suspended)
            && StartDate <= utcNow
            && EndDate > utcNow;

        public string GetDisplayStatus(DateTime utcNow)
        {
            if (StartDate > utcNow)
            {
                return Status == SubscriptionStatus.Cancelled ? "Cancellation Scheduled" : "Scheduled";
            }

            if (Status == SubscriptionStatus.Cancelled)
            {
                return "Cancelled";
            }

            if (EndDate <= utcNow)
            {
                return "Expired";
            }

            return Status == SubscriptionStatus.Suspended ? "Suspended" : "Active";
        }
    }
}
namespace freshcrumbs.CRM.winforms.Models
{
    public class PlanModel
    {
        public int PlanId { get; set; }

        public string PlanCode { get; set; } = string.Empty;

        public string DisplayName { get; set; } = string.Empty;

        public string Description { get; set; } = string.Empty;

        public decimal Price { get; set; }

        public string BillingCycle { get; set; } = "Monthly";

        public List<string> Features { get; set; } = new();

        public int MaxUsers { get; set; }

        public bool BranchingEnabled { get; set; }

        public int? MaxBranches { get; set; }

        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; }

        public DateTime? UpdatedAt { get; set; }

        public string StatusText => IsActive ? "Active" : "Inactive";

        public string BranchesText => BranchingEnabled && MaxBranches != null ? MaxBranches.Value.ToString() : "N/A";

        public string FeaturesText => string.Join(", ", Features.Select(PlanFeatureCatalog.DisplayName));
    }

    public static class PlanFeatureCatalog
    {
        public static readonly (string Key, string DisplayName)[] All =
        {
            ("MainTransactions", "Main Transactions"),
            ("DataCollection", "Data Collection"),
            ("BusinessIntelligence", "Business Intelligence"),
            ("ActionsRetention", "Actions / Retention"),
            ("Branching", "Branching")
        };

        // Module rows shown on the plan cards. Each row is driven by the existing plan feature keys.
        public static readonly (string Label, string FeatureKey)[] ModuleRows =
        {
            ("Customer Management", "MainTransactions"),
            ("Product Management", "MainTransactions"),
            ("Sales Transaction", "MainTransactions"),
            ("Promotion Management", "ActionsRetention"),
            ("Loyalty Management", "ActionsRetention"),
            ("Feedback and Concern Management", "DataCollection"),
            ("Inquiries Management", "DataCollection"),
            ("BI", "BusinessIntelligence")
        };

        public static string DisplayName(string key)
        {
            foreach (var feature in All)
            {
                if (feature.Key == key)
                {
                    return feature.DisplayName;
                }
            }

            return key;
        }
    }
}
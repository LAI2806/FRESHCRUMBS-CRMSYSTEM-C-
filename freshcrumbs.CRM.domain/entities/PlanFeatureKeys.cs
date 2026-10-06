namespace freshcrumbs.CRM.domain.entities
{
    public static class PlanFeatureKeys
    {
        public const string MainTransactions = "MainTransactions";
        public const string DataCollection = "DataCollection";
        public const string BusinessIntelligence = "BusinessIntelligence";
        public const string ActionsRetention = "ActionsRetention";
        public const string Branching = "Branching";

        public static readonly IReadOnlyList<string> All = new[]
        {
            MainTransactions,
            DataCollection,
            BusinessIntelligence,
            ActionsRetention,
            Branching
        };

        public static string DisplayName(string key) => key switch
        {
            MainTransactions => "Main Transactions",
            DataCollection => "Data Collection",
            BusinessIntelligence => "Business Intelligence",
            ActionsRetention => "Actions / Retention",
            Branching => "Branching",
            _ => key
        };
    }
}
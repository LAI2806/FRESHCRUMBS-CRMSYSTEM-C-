using freshcrumbs.CRM.domain.entities;

namespace freshcrumbs.CRM.api.Authorization
{
    // Single place that decides which plan feature each tenant API area requires.
    // Add new tenant controllers here. Areas not listed only require an active subscription.
    public static class TenantFeatureMap
    {
        private static readonly Dictionary<string, string> Map = new(StringComparer.OrdinalIgnoreCase)
        {
            ["products"] = PlanFeatureKeys.MainTransactions,
            ["sales"] = PlanFeatureKeys.MainTransactions,
            ["customers"] = PlanFeatureKeys.MainTransactions,
            ["feedback"] = PlanFeatureKeys.DataCollection,
            ["inquiries"] = PlanFeatureKeys.DataCollection,
            ["loyalty"] = PlanFeatureKeys.ActionsRetention,
            ["promotions"] = PlanFeatureKeys.ActionsRetention,
            ["reports"] = PlanFeatureKeys.BusinessIntelligence
        };

        public static string? RequiredFeature(string? segment)
        {
            return segment != null && Map.TryGetValue(segment, out var feature) ? feature : null;
        }
    }
}
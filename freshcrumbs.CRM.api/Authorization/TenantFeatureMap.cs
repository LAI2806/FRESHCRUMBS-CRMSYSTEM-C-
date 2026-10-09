using freshcrumbs.CRM.domain.entities;

namespace freshcrumbs.CRM.api.Authorization
{
    // Single place that decides which plan feature each tenant API area requires.
    // Add new tenant controllers here. Areas not listed only require an active subscription.
    public static class TenantFeatureMap
    {
        private static readonly HashSet<string> DashboardEndpoints = new(StringComparer.OrdinalIgnoreCase)
        {
            "dashboard",
            "overview",
            "charts",
            "branches",
            "operations"
        };

        private static readonly Dictionary<string, string> Map = new(StringComparer.OrdinalIgnoreCase)
        {
            ["products"] = PlanFeatureKeys.MainTransactions,
            ["sales"] = PlanFeatureKeys.MainTransactions,
            ["customers"] = PlanFeatureKeys.MainTransactions,
            ["feedback"] = PlanFeatureKeys.DataCollection,
            ["inquiries"] = PlanFeatureKeys.DataCollection,
            ["loyalty"] = PlanFeatureKeys.ActionsRetention,
            ["promotions"] = PlanFeatureKeys.ActionsRetention,
            ["reports"] = PlanFeatureKeys.BusinessIntelligence,
            ["branches"] = PlanFeatureKeys.Branching
        };

        public static string? RequiredFeature(string? segment, string? subSegment = null)
        {
            // The dashboard endpoints (dashboard, overview, charts, branches, operations) are available on every plan; what they
            // return is shaped per role/plan in the controller. Report generation is also on every plan (Basic Reports):
            // each report type is checked against its own Report.* permission. Other report endpoints need Business Intelligence.
            if (string.Equals(segment, "reports", StringComparison.OrdinalIgnoreCase)
                && subSegment != null
                && (DashboardEndpoints.Contains(subSegment) || string.Equals(subSegment, "generate", StringComparison.OrdinalIgnoreCase)))
            {
                return null;
            }

            return segment != null && Map.TryGetValue(segment, out var feature) ? feature : null;
        }
    }
}
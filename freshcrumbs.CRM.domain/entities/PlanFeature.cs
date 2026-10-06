namespace freshcrumbs.CRM.domain.entities
{
    public class PlanFeature
    {
        public int PlanId { get; set; }

        public string FeatureKey { get; set; } = string.Empty;

        public Plan? Plan { get; set; }
    }
}
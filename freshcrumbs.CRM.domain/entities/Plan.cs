namespace freshcrumbs.CRM.domain.entities
{
    public class Plan
    {
        public int PlanId { get; set; }

        public string PlanCode { get; set; } = string.Empty;

        public string DisplayName { get; set; } = string.Empty;

        public string Description { get; set; } = string.Empty;

        public decimal Price { get; set; }

        public BillingCycle BillingCycle { get; set; } = BillingCycle.Monthly;

        public int MaxUsers { get; set; }

        public bool BranchingEnabled { get; set; }

        public int? MaxBranches { get; set; }

        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? UpdatedAt { get; set; }

        public ICollection<PlanFeature> Features { get; set; } = new List<PlanFeature>();
    }
}
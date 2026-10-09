namespace freshcrumbs.CRM.domain.entities
{
    public class CustomerDiscountEligibility : ISyncEntity
    {
        // Sync identity (offline-first): global id used to match records between local and cloud databases.
        public Guid RowGuid { get; set; } = Guid.NewGuid();

        // UTC time of the last change; used for conflict resolution and incremental pull.
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        public static readonly string[] Categories =
        {
            "Senior Citizen",
            "PWD",
            "Other Eligible Category"
        };

        public static readonly string[] VerificationStatuses =
        {
            "Pending Verification",
            "Verified",
            "Rejected"
        };

        public int EligibilityId { get; set; }

        public int CustomerId { get; set; }

        // One of CustomerDiscountEligibility.Categories, e.g. "Senior Citizen".
        public string Category { get; set; } = string.Empty;

        public string IdNumber { get; set; } = string.Empty;

        // One of CustomerDiscountEligibility.VerificationStatuses.
        public string VerificationStatus { get; set; } = "Pending Verification";

        public Customer? Customer { get; set; }
    }
}
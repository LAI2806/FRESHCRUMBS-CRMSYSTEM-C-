namespace freshcrumbs.CRM.domain.entities
{
    public class TermsAcceptance
    {
        public int TermsAcceptanceId { get; set; }

        public int TermsVersionId { get; set; }

        public TermsVersion? TermsVersion { get; set; }

        public int CompanyId { get; set; }

        public Company? Company { get; set; }

        // The accepting user is stored as a snapshot so the record survives later user changes.
        public string UserId { get; set; } = string.Empty;

        public string UserName { get; set; } = string.Empty;

        public DateTime AcceptedAt { get; set; } = DateTime.UtcNow;
    }
}
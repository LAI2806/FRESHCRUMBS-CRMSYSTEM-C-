namespace freshcrumbs.CRM.domain.entities
{
    public class TermsVersion
    {
        public int TermsVersionId { get; set; }

        // Sequential, human-readable version identifier (v1, v2, ...). Assigned when the draft is created.
        public int VersionNumber { get; set; }

        public string Title { get; set; } = string.Empty;

        public string Content { get; set; } = string.Empty;

        public TermsStatus Status { get; set; } = TermsStatus.Draft;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public string CreatedBy { get; set; } = string.Empty;

        public DateTime? UpdatedAt { get; set; }

        public DateTime? PublishedAt { get; set; }

        public string? PublishedBy { get; set; }

        public DateTime? ArchivedAt { get; set; }

        // Chosen when the version is published: must every company accept it before using the CRM?
        public bool RequiresAcceptance { get; set; }
    }
}
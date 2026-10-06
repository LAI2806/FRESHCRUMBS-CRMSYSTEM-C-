namespace freshcrumbs.CRM.winforms.Models
{
    public class TermsVersionModel
    {
        public int TermsVersionId { get; set; }

        public int VersionNumber { get; set; }

        public string Title { get; set; } = string.Empty;

        public string Content { get; set; } = string.Empty;

        public string Status { get; set; } = "Draft";

        public DateTime CreatedAt { get; set; }

        public string CreatedBy { get; set; } = string.Empty;

        public DateTime? UpdatedAt { get; set; }

        public DateTime? PublishedAt { get; set; }

        public string? PublishedBy { get; set; }

        public DateTime? ArchivedAt { get; set; }

        public bool RequiresAcceptance { get; set; }

        public int AcceptedCompanies { get; set; }

        public string VersionText => $"v{VersionNumber}";

        public string CreatedText => CreatedAt.ToString("yyyy-MM-dd");

        public string PublishedText => PublishedAt == null ? "-" : PublishedAt.Value.ToString("yyyy-MM-dd");

        public string AcceptanceText => Status == "Draft" ? "-" : AcceptedCompanies.ToString();
    }

    public class TermsAcceptanceModel
    {
        public int CompanyId { get; set; }

        public string CompanyCode { get; set; } = string.Empty;

        public string CompanyName { get; set; } = string.Empty;

        public string UserName { get; set; } = string.Empty;

        public DateTime AcceptedAt { get; set; }

        public string AcceptedText => AcceptedAt.ToString("yyyy-MM-dd HH:mm") + " UTC";
    }

    public class TenantTermsModel
    {
        public bool HasTerms { get; set; }

        public int TermsVersionId { get; set; }

        public int VersionNumber { get; set; }

        public string Title { get; set; } = string.Empty;

        public string Content { get; set; } = string.Empty;

        public DateTime? PublishedAt { get; set; }

        public bool RequiresAcceptance { get; set; }

        public bool Accepted { get; set; }

        public bool PendingAcceptance { get; set; }
    }
}
namespace freshcrumbs.CRM.domain.entities
{
    // Cloud side: one row per processed OperationId. Makes push idempotent (no duplicates when a
    // device retries after an interrupted connection) and records conflicts that were resolved.
    public class SyncReceipt
    {
        public Guid OperationId { get; set; }

        public string EntityType { get; set; } = string.Empty;

        public Guid EntityRowGuid { get; set; }

        // Applied / Skipped (older than cloud version) / Rejected
        public string Result { get; set; } = "Applied";

        public string? Detail { get; set; }

        public DateTime ProcessedAtUtc { get; set; } = DateTime.UtcNow;
    }
}
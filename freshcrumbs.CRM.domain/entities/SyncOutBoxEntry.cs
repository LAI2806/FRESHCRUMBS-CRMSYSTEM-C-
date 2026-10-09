namespace freshcrumbs.CRM.domain.entities
{
    public static class SyncOperationType
    {
        public const string Upsert = "Upsert";
        public const string Delete = "Delete";
    }

    public static class SyncOutboxStatus
    {
        public const string Pending = "Pending";
        public const string Synced = "Synced";
        // Rejected by the cloud permanently (e.g. subscription feature not included). Kept for review.
        public const string Rejected = "Rejected";
    }

    // One local change waiting to be sent to the cloud. Written in the SAME database transaction
    // as the data change itself, so a closed application or crash can never lose a change.
    public class SyncOutboxEntry
    {
        // Strictly increasing: defines the order in which changes are replayed in the cloud.
        public long Sequence { get; set; }

        // Idempotency key: the cloud remembers processed OperationIds, so a retry never duplicates.
        public Guid OperationId { get; set; } = Guid.NewGuid();

        // All changes saved by one API request share a GroupId and are applied in ONE cloud transaction
        // (e.g. a sale and its loyalty entry), so they succeed or fail together.
        public Guid GroupId { get; set; }

        public string EntityType { get; set; } = string.Empty;

        public Guid EntityRowGuid { get; set; }

        public string Operation { get; set; } = SyncOperationType.Upsert;

        // JSON snapshot of the entity (parents referenced by RowGuid) plus quantity/points deltas.
        public string Payload { get; set; } = "{}";

        public DateTime ChangedAtUtc { get; set; } = DateTime.UtcNow;

        public string Status { get; set; } = SyncOutboxStatus.Pending;

        public int Attempts { get; set; }

        public string? LastError { get; set; }

        public DateTime? SyncedAtUtc { get; set; }
    }
}
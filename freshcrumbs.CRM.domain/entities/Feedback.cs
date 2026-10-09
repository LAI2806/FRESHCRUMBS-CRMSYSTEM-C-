using System;

namespace freshcrumbs.CRM.domain.entities
{
    public class Feedback : ISyncEntity
    {
        // Sync identity (offline-first): global id used to match records between local and cloud databases.
        public Guid RowGuid { get; set; } = Guid.NewGuid();

        // UTC time of the last change; used for conflict resolution and incremental pull.
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        public int FeedbackId { get; set; }

        public int CustomerId { get; set; }

        public string Type { get; set; } = string.Empty;

        public string Category { get; set; } = string.Empty;

        public string Comment { get; set; } = string.Empty;

        public DateTime DateSubmitted { get; set; } = DateTime.UtcNow;

        public string Status { get; set; } = "Pending";

        public bool IsDeleted { get; set; } = false;

        // PREMIUM (Branching) only: the branch where it was recorded (set by the server). Null for older records.
        public int? BranchId { get; set; }

        // Not stored. Branch name for display.
        public string? BranchName { get; set; }

        public Customer? Customer { get; set; }
    }
}
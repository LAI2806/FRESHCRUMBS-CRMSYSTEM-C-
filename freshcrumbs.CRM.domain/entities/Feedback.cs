using System;

namespace freshcrumbs.CRM.domain.entities
{
    public class Feedback
    {
        public int FeedbackId { get; set; }

        public int CustomerId { get; set; }

        public string Type { get; set; } = string.Empty;

        public string Category { get; set; } = string.Empty;

        public string Comment { get; set; } = string.Empty;

        public DateTime DateSubmitted { get; set; } = DateTime.UtcNow;

        public string Status { get; set; } = "Pending";

        public bool IsDeleted { get; set; } = false;

        public Customer? Customer { get; set; }
    }
}
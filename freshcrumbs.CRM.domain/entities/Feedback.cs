using System;

namespace freshcrumbs.CRM.domain.entities
{
    public class Feedback
    {
        public int FeedbackId { get; set; }

        public int CustomerId { get; set; }

        public string Type { get; set; } = string.Empty;

        public string Subject { get; set; } = string.Empty;

        public string Description { get; set; } = string.Empty;

        public DateTime DateSubmitted { get; set; } = DateTime.UtcNow;

        public string Status { get; set; } = "Pending";

        public Customer? Customer { get; set; }
    }
}
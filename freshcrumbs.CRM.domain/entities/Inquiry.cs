using System;

namespace freshcrumbs.CRM.domain.entities
{
    public class Inquiry
    {
        public int InquiryId { get; set; }

        public int CustomerId { get; set; }

        public string Type { get; set; } = string.Empty;

        public string Source { get; set; } = string.Empty;

        public string Subject { get; set; } = string.Empty;

        public string Message { get; set; } = string.Empty;

        public DateTime DateSubmitted { get; set; } = DateTime.UtcNow;

        public string Status { get; set; } = "Pending";

        public string Response { get; set; } = string.Empty;

        public string RespondedBy { get; set; } = string.Empty;

        public DateTime? RespondedAt { get; set; }

        public bool IsDeleted { get; set; } = false;

        public Customer? Customer { get; set; }
    }
}
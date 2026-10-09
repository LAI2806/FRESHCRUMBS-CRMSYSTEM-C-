namespace freshcrumbs.CRM.winforms.Models
{
    public class FeedbackModel
    {
        public int FeedbackId { get; set; }

        public int CustomerId { get; set; }

        public string Type { get; set; } = string.Empty;

        public string Category { get; set; } = string.Empty;

        public string Comment { get; set; } = string.Empty;

        public DateTime DateSubmitted { get; set; } = DateTime.Now;

        public string Status { get; set; } = "Pending";

        // PREMIUM: branch where it was recorded (set by the server) and its name for display.
        public int? BranchId { get; set; }

        public string? BranchName { get; set; }
    }
}
namespace freshcrumbs.CRM.winforms.Models
{
    public class InquiryModel
    {
        public int InquiryId { get; set; }
        public int CustomerId { get; set; }
        public string Subject { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public DateTime DateSubmitted { get; set; } = DateTime.Now;
        public string Status { get; set; } = "Pending";
        public string Response { get; set; } = string.Empty;
        public string RespondedBy { get; set; } = string.Empty;
        public DateTime? RespondedAt { get; set; }
    }
}
namespace freshcrumbs.CRM.winforms.Models
{
    public class BranchAssignmentModel
    {
        public string UserId { get; set; } = string.Empty;

        public string UserName { get; set; } = string.Empty;

        public string FullName { get; set; } = string.Empty;

        public string Role { get; set; } = string.Empty;

        public string Status { get; set; } = string.Empty;

        public int? BranchId { get; set; }

        public string? BranchName { get; set; }
    }
}

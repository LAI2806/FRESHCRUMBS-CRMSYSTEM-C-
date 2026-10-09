namespace freshcrumbs.CRM.winforms.Models
{
    public class BranchModel
    {
        public int BranchId { get; set; }

        public string BranchName { get; set; } = string.Empty;

        public string Address { get; set; } = string.Empty;

        public string Status { get; set; } = "Active";

        // The MANAGER-role account assigned to this branch (derived from the branch assignments); null = no manager.
        public string? ManagerUserId { get; set; }

        public string? ManagerName { get; set; }

        public string ManagerDisplay => ManagerName ?? "(None)";

        public override string ToString() => BranchName;
    }
}

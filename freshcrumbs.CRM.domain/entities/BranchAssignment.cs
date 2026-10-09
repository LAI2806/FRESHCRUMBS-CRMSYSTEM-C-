namespace freshcrumbs.CRM.domain.entities
{
    // Which branch an employee works at. UserId is the ApplicationUser.Id from the master database
    // (users are company-wide; branches live in the tenant database). BranchId = null means unassigned,
    // so un-assigning is an ordinary update that synchronizes like any other change.
    public class BranchAssignment : ISyncEntity
    {
        public Guid RowGuid { get; set; } = Guid.NewGuid();

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        public int BranchAssignmentId { get; set; }

        public string UserId { get; set; } = string.Empty;

        public string UserName { get; set; } = string.Empty;

        public int? BranchId { get; set; }

        public Branch? Branch { get; set; }
    }
}

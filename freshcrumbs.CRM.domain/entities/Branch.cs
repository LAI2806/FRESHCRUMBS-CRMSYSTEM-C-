namespace freshcrumbs.CRM.domain.entities
{
    public class Branch : ISyncEntity
    {
        public Guid RowGuid { get; set; } = Guid.NewGuid();

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        public int BranchId { get; set; }

        public string BranchName { get; set; } = string.Empty;

        public string Address { get; set; } = string.Empty;

        public string Status { get; set; } = "Active";

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}

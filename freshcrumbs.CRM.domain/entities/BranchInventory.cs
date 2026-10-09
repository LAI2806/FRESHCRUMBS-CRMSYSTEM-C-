namespace freshcrumbs.CRM.domain.entities
{
    // Stock of one shared Product at one Branch (PREMIUM / Branching plans).
    // Product.Quantity stays the company-wide total; this row is the authority for the branch.
    public class BranchInventory : ISyncEntity
    {
        public Guid RowGuid { get; set; } = Guid.NewGuid();

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        public int BranchInventoryId { get; set; }

        public int BranchId { get; set; }

        public int ProductId { get; set; }

        public int Quantity { get; set; }

        public Branch? Branch { get; set; }

        public Product? Product { get; set; }
    }
}

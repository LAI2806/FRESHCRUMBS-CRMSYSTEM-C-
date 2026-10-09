namespace freshcrumbs.CRM.domain.entities
{
    public class TransactionItem : ISyncEntity
    {
        // Sync identity (offline-first): global id used to match records between local and cloud databases.
        public Guid RowGuid { get; set; } = Guid.NewGuid();

        // UTC time of the last change; used for conflict resolution and incremental pull.
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        public int TransactionItemId { get; set; }

        public int TransactionId { get; set; }

        public int ProductId { get; set; }

        public int Quantity { get; set; }

        public decimal UnitPrice { get; set; }

        public decimal Subtotal { get; set; }

        public SalesTransaction? SalesTransaction { get; set; }

        public Product? Product { get; set; }
    }
}
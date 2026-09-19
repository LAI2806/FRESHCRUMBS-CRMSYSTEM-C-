namespace freshcrumbs.CRM.domain.entities
{
    public class TransactionItem
    {
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
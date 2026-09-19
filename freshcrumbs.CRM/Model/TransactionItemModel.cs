namespace freshcrumbs.CRM.winforms.Models
{
    public class TransactionItemModel
    {
        public int TransactionItemId { get; set; }

        public int TransactionId { get; set; }

        public int ProductId { get; set; }

        public int Quantity { get; set; }

        public decimal UnitPrice { get; set; }

        public decimal Subtotal { get; set; }
    }
}
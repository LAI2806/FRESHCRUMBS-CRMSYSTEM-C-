namespace freshcrumbs.CRM.winforms.Models
{
    public class CustomerModel
    {
        public int CustomerId { get; set; }

        public string CustomerCode { get; set; } = string.Empty;

        public string FirstName { get; set; } = string.Empty;

        public string LastName { get; set; } = string.Empty;

        public string Email { get; set; } = string.Empty;

        public string ContactNo { get; set; } = string.Empty;

        public string Address { get; set; } = string.Empty;

        public int LoyaltyPoints { get; set; }

        public string Status { get; set; } = string.Empty;
    }
}
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

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

        public List<CustomerDiscountEligibilityModel> DiscountEligibilities { get; set; } = new List<CustomerDiscountEligibilityModel>();

        // Computed for the Customer grid only (e.g. "Senior Citizen - Verified, PWD - Verified").
        // Not a database field and not sent to the API.
        [JsonIgnore]
        public string DiscountEligibilitySummary =>
            DiscountEligibilities == null || DiscountEligibilities.Count == 0
                ? string.Empty
                : string.Join(", ", DiscountEligibilities.Select(e =>
                    string.IsNullOrWhiteSpace(e.VerificationStatus)
                        ? e.Category
                        : $"{e.Category} - {e.VerificationStatus}"));
    }

    public class CustomerDiscountEligibilityModel
    {
        public int EligibilityId { get; set; }

        public int CustomerId { get; set; }

        public string Category { get; set; } = string.Empty;

        public string IdNumber { get; set; } = string.Empty;

        public string VerificationStatus { get; set; } = string.Empty;
    }
}
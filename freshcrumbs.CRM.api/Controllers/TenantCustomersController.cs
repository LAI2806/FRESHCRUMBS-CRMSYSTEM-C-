using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using freshcrumbs.CRM.domain.entities;
using freshcrumbs.CRM.infrastructure.services;
using Microsoft.EntityFrameworkCore;

namespace freshcrumbs.CRM.api.Controllers
{
    [ApiController]
    [Route("api/tenant/{companyId:int}/customers")]
    public class TenantCustomersController : ControllerBase
    {
        private readonly ITenantDbContextFactory _tenantFactory;

        public TenantCustomersController(ITenantDbContextFactory tenantFactory)
        {
            _tenantFactory = tenantFactory;
        }

        [HttpGet]
        public async Task<IActionResult> GetCustomers(int companyId, bool includeInactive = false)
        {
            await using var tenantDb = await _tenantFactory.CreateAsync(companyId);

            IQueryable<Customer> query = tenantDb.Customers
                .AsNoTracking()
                .Include(x => x.DiscountEligibilities);

            if (!includeInactive)
            {
                query = query.Where(x => x.Status == "Active");
            }

            var customers = await query
                .OrderBy(x => x.CustomerId)
                .ToListAsync();

            var databaseName = tenantDb.Database.GetDbConnection().Database;
            Console.WriteLine($"API DATABASE: {databaseName}");

            return Ok(customers);

        }

        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetCustomerById(int companyId, int id)
        {
            await using var tenantDb = await _tenantFactory.CreateAsync(companyId);

            var customer = await tenantDb.Customers
                .AsNoTracking()
                .Include(x => x.DiscountEligibilities)
                .FirstOrDefaultAsync(x => x.CustomerId == id);

            if (customer == null)
            {
                return NotFound($"Customer with id {id} not found.");
            }

            return Ok(customer);

        }

        [HttpPost]
        public async Task<IActionResult> CreateCustomer(int companyId, Customer customer)
        {
            await using var tenantDb = await _tenantFactory.CreateAsync(companyId);

            string? eligibilityError = NormalizeEligibilities(customer.DiscountEligibilities);
            if (eligibilityError != null)
            {
                return BadRequest(eligibilityError);
            }

            foreach (var eligibility in customer.DiscountEligibilities)
            {
                eligibility.EligibilityId = 0;
                eligibility.CustomerId = 0;
            }

            tenantDb.Customers.Add(customer);
            await tenantDb.SaveChangesAsync();

            return Created(
                $"api/tenant/{companyId}/customers/{customer.CustomerId}",
                customer);
        }

        [HttpPut("{id:int}")]
        public async Task<IActionResult> UpdateCustomer(int companyId, int id, Customer updated)
        {
            await using var tenantDb = await _tenantFactory.CreateAsync(companyId);

            var customer = await tenantDb.Customers
                .Include(x => x.DiscountEligibilities)
                .FirstOrDefaultAsync(x => x.CustomerId == id);

            if (customer == null)
            {
                return NotFound($"Customer with id {id} not found.");
            }

            string? eligibilityError = NormalizeEligibilities(updated.DiscountEligibilities);
            if (eligibilityError != null)
            {
                return BadRequest(eligibilityError);
            }

            customer.CustomerCode = updated.CustomerCode;
            customer.FirstName = updated.FirstName;
            customer.LastName = updated.LastName;
            customer.Email = updated.Email;
            customer.ContactNo = updated.ContactNo;
            customer.Address = updated.Address;
            customer.LoyaltyPoints = updated.LoyaltyPoints;
            customer.Status = updated.Status;

            var eligibilitiesToRemove = customer.DiscountEligibilities
                .Where(existing => !updated.DiscountEligibilities.Any(u =>
                    string.Equals(u.Category, existing.Category, StringComparison.OrdinalIgnoreCase)))
                .ToList();

            foreach (var eligibility in eligibilitiesToRemove)
            {
                customer.DiscountEligibilities.Remove(eligibility);
                tenantDb.CustomerDiscountEligibilities.Remove(eligibility);
            }

            foreach (var incoming in updated.DiscountEligibilities)
            {
                var existing = customer.DiscountEligibilities.FirstOrDefault(e =>
                    string.Equals(e.Category, incoming.Category, StringComparison.OrdinalIgnoreCase));

                if (existing == null)
                {
                    customer.DiscountEligibilities.Add(new CustomerDiscountEligibility
                    {
                        Category = incoming.Category,
                        IdNumber = incoming.IdNumber,
                        VerificationStatus = incoming.VerificationStatus
                    });
                }
                else
                {
                    existing.IdNumber = incoming.IdNumber;
                    existing.VerificationStatus = incoming.VerificationStatus;
                }
            }

            await tenantDb.SaveChangesAsync();

            return Ok(customer);
        }

        [HttpDelete("{id:int}")]
        public async Task<IActionResult> DeleteCustomer(int companyId, int id)
        {
            await using var tenantDb = await _tenantFactory.CreateAsync(companyId);

            var customer = await tenantDb.Customers.FirstOrDefaultAsync(x => x.CustomerId == id);

            if (customer == null)
            {
                return NotFound($"Customer with id {id} not found.");
            }

            customer.Status = "Inactive";
            await tenantDb.SaveChangesAsync();

            return NoContent();
        }

        [HttpPut("{id:int}/reactivate")]
        public async Task<IActionResult> ReactivateCustomer(int companyId, int id)
        {
            await using var tenantDb = await _tenantFactory.CreateAsync(companyId);

            var customer = await tenantDb.Customers.FirstOrDefaultAsync(x => x.CustomerId == id);

            if (customer == null)
            {
                return NotFound($"Customer with id {id} not found.");
            }

            customer.Status = "Active";
            await tenantDb.SaveChangesAsync();

            return Ok(customer);
        }

        // Validates each eligibility record and rejects duplicate categories for the same customer.
        // Normalizes Category/VerificationStatus to their canonical casing in place.
        private static string? NormalizeEligibilities(ICollection<CustomerDiscountEligibility>? eligibilities)
        {
            if (eligibilities == null || eligibilities.Count == 0)
            {
                return null;
            }

            var seenCategories = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var eligibility in eligibilities)
            {
                var category = Array.Find(CustomerDiscountEligibility.Categories,
                    c => string.Equals(c, eligibility.Category?.Trim(), StringComparison.OrdinalIgnoreCase));

                if (category == null)
                {
                    return $"\"{eligibility.Category}\" is not a valid discount eligibility category.";
                }

                if (!seenCategories.Add(category))
                {
                    return $"Duplicate discount eligibility \"{category}\" for this customer.";
                }

                var status = Array.Find(CustomerDiscountEligibility.VerificationStatuses,
                    s => string.Equals(s, eligibility.VerificationStatus?.Trim(), StringComparison.OrdinalIgnoreCase));

                if (status == null)
                {
                    return $"\"{eligibility.VerificationStatus}\" is not a valid verification status for {category}.";
                }

                var idNumber = eligibility.IdNumber?.Trim() ?? string.Empty;

                if (idNumber.Length == 0)
                {
                    return $"An ID number is required for {category} eligibility.";
                }

                if (idNumber.Length > 50)
                {
                    return $"The ID number for {category} eligibility cannot exceed 50 characters.";
                }

                eligibility.Category = category;
                eligibility.VerificationStatus = status;
                eligibility.IdNumber = idNumber;
            }

            return null;
        }
    }
}
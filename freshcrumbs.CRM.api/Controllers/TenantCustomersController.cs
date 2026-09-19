using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using freshcrumbs.CRM.domain.entities;
using freshcrumbs.CRM.infrastructure.services;

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

            var query = tenantDb.Customers.AsNoTracking();

            if (!includeInactive)
            {
                query = query.Where(x => x.Status == "Active");
            }

            var customers = await query
                .OrderBy(x => x.CustomerId)
                .ToListAsync();

            return Ok(customers);
        }

        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetCustomerById(int companyId, int id)
        {
            await using var tenantDb = await _tenantFactory.CreateAsync(companyId);

            var customer = await tenantDb.Customers
                .AsNoTracking()
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

            var customer = await tenantDb.Customers.FirstOrDefaultAsync(x => x.CustomerId == id);

            if (customer == null)
            {
                return NotFound($"Customer with id {id} not found.");
            }

            customer.CustomerCode = updated.CustomerCode;
            customer.FirstName = updated.FirstName;
            customer.LastName = updated.LastName;
            customer.Email = updated.Email;
            customer.ContactNo = updated.ContactNo;
            customer.Address = updated.Address;
            customer.LoyaltyPoints = updated.LoyaltyPoints;
            customer.Status = updated.Status;

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
    }
}
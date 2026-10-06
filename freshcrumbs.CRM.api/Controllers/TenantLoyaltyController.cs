using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using freshcrumbs.CRM.domain.entities;
using freshcrumbs.CRM.infrastructure.services;

namespace freshcrumbs.CRM.api.Controllers
{
    [ApiController]
    [Route("api/tenant/{companyId:int}/loyalty")]
    public class TenantLoyaltyController : ControllerBase
    {
        private readonly ITenantDbContextFactory _tenantFactory;

        public TenantLoyaltyController(ITenantDbContextFactory tenantFactory)
        {
            _tenantFactory = tenantFactory;
        }

        [HttpGet]
        public async Task<IActionResult> GetLoyaltyTransactions(int companyId, bool includeDeleted = false)
        {
            await using var tenantDb = await _tenantFactory.CreateAsync(companyId);

            var query = tenantDb.LoyaltyTransactions.AsNoTracking();

            if (!includeDeleted)
            {
                query = query.Where(x => !x.IsDeleted);
            }

            var transactions = await query
                .OrderBy(x => x.LoyaltyTransactionId)
                .ToListAsync();

            return Ok(transactions);
        }

        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetLoyaltyTransactionById(int companyId, int id)
        {
            await using var tenantDb = await _tenantFactory.CreateAsync(companyId);

            var transaction = await tenantDb.LoyaltyTransactions
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.LoyaltyTransactionId == id);

            if (transaction == null)
            {
                return NotFound($"LoyaltyTransaction with id {id} not found.");
            }

            return Ok(transaction);
        }

        [HttpPost]
        public async Task<IActionResult> CreateLoyaltyTransaction(int companyId, LoyaltyTransaction transaction)
        {
            await using var tenantDb = await _tenantFactory.CreateAsync(companyId);

            var customer = await tenantDb.Customers
                .FirstOrDefaultAsync(x => x.CustomerId == transaction.CustomerId);

            if (customer == null)
            {
                return BadRequest($"CustomerId {transaction.CustomerId} does not exist for this tenant.");
            }

            customer.LoyaltyPoints += transaction.PointsEarned - transaction.PointsUsed;

            tenantDb.LoyaltyTransactions.Add(transaction);
            await tenantDb.SaveChangesAsync();

            return Created(
                $"api/tenant/{companyId}/loyalty/{transaction.LoyaltyTransactionId}",
                transaction);
        }

        [HttpPut("{id:int}")]
        public async Task<IActionResult> UpdateLoyaltyTransaction(int companyId, int id, LoyaltyTransaction updated)
        {
            await using var tenantDb = await _tenantFactory.CreateAsync(companyId);

            var transaction = await tenantDb.LoyaltyTransactions
                .FirstOrDefaultAsync(x => x.LoyaltyTransactionId == id);

            if (transaction == null)
            {
                return NotFound($"LoyaltyTransaction with id {id} not found.");
            }

            var customer = await tenantDb.Customers
                .FirstOrDefaultAsync(x => x.CustomerId == transaction.CustomerId);

            if (customer == null)
            {
                return BadRequest("Linked customer no longer exists.");
            }

            int oldDelta = transaction.PointsEarned - transaction.PointsUsed;
            int newDelta = updated.PointsEarned - updated.PointsUsed;

            customer.LoyaltyPoints += (newDelta - oldDelta);

            transaction.PointsEarned = updated.PointsEarned;
            transaction.PointsUsed = updated.PointsUsed;
            transaction.TransactionType = updated.TransactionType;
            transaction.Date = updated.Date;

            await tenantDb.SaveChangesAsync();

            return Ok(transaction);
        }

        [HttpDelete("{id:int}")]
        public async Task<IActionResult> DeleteLoyaltyTransaction(int companyId, int id)
        {
            await using var tenantDb = await _tenantFactory.CreateAsync(companyId);

            var transaction = await tenantDb.LoyaltyTransactions
                .FirstOrDefaultAsync(x => x.LoyaltyTransactionId == id);

            if (transaction == null)
            {
                return NotFound($"LoyaltyTransaction with id {id} not found.");
            }

            if (transaction.IsDeleted)
            {
                return Conflict("This loyalty transaction has already been deleted.");
            }

            var customer = await tenantDb.Customers
                .FirstOrDefaultAsync(x => x.CustomerId == transaction.CustomerId);

            if (customer != null)
            {
                customer.LoyaltyPoints -= (transaction.PointsEarned - transaction.PointsUsed);
            }

            transaction.IsDeleted = true;
            await tenantDb.SaveChangesAsync();

            return NoContent();
        }
    }
}
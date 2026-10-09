using freshcrumbs.CRM.api.Authorization;
using freshcrumbs.CRM.api.Services;
using freshcrumbs.CRM.infrastructure.data;
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
        [RequireTenantPermission(TenantPermissions.ManageLoyalty)]
        public async Task<IActionResult> GetLoyaltyTransactions(int companyId, bool includeDeleted = false)
        {
            await using var tenantDb = await _tenantFactory.CreateAsync(companyId);

            var query = tenantDb.LoyaltyTransactions.AsNoTracking();

            if (!includeDeleted)
            {
                query = query.Where(x => !x.IsDeleted);
            }

            var rows = await Project(query.OrderBy(x => x.LoyaltyTransactionId), tenantDb).ToListAsync();
            var scope = await BranchStock.GetScopeAsync(HttpContext, tenantDb);

            return Ok(rows.Select(r => r with { CanModify = CanModify(scope, r.SalesTransactionId, r.BranchId) }));
        }

        [HttpGet("{id:int}")]
        [RequireTenantPermission(TenantPermissions.ManageLoyalty)]
        public async Task<IActionResult> GetLoyaltyTransactionById(int companyId, int id)
        {
            await using var tenantDb = await _tenantFactory.CreateAsync(companyId);

            var row = await Project(tenantDb.LoyaltyTransactions.AsNoTracking().Where(x => x.LoyaltyTransactionId == id), tenantDb)
                .FirstOrDefaultAsync();

            if (row == null)
            {
                return NotFound($"LoyaltyTransaction with id {id} not found.");
            }

            var scope = await BranchStock.GetScopeAsync(HttpContext, tenantDb);

            return Ok(row with { CanModify = CanModify(scope, row.SalesTransactionId, row.BranchId) });
        }

        [HttpPost]
        [RequireTenantPermission(TenantPermissions.ManageLoyalty)]
        public async Task<IActionResult> CreateLoyaltyTransaction(int companyId, LoyaltyTransaction transaction)
        {
            string? error = ValidatePoints(transaction);
            if (error != null)
            {
                return BadRequest(InputRules.Message(error));
            }

            if (!InputRules.IsRecordDate(transaction.Date == default ? DateTime.UtcNow : transaction.Date))
            {
                return BadRequest(InputRules.Message("The date must be a valid date and cannot be in the future."));
            }

            await using var tenantDb = await _tenantFactory.CreateAsync(companyId);

            var customer = await tenantDb.Customers
                .FirstOrDefaultAsync(x => x.CustomerId == transaction.CustomerId);

            if (customer == null)
            {
                return BadRequest($"CustomerId {transaction.CustomerId} does not exist for this tenant.");
            }

            // Identity and links are decided here, never taken from the client.
            transaction.LoyaltyTransactionId = 0;
            transaction.RowGuid = Guid.NewGuid();
            transaction.UpdatedAt = DateTime.UtcNow;
            transaction.IsDeleted = false;
            transaction.Customer = null;
            transaction.SalesTransaction = null;

            if (transaction.Date == default)
            {
                transaction.Date = DateTime.UtcNow;
            }

            int? saleBranchId = null;

            if (transaction.SalesTransactionId != null)
            {
                var sale = await tenantDb.SalesTransactions
                    .AsNoTracking()
                    .FirstOrDefaultAsync(x => x.TransactionId == transaction.SalesTransactionId.Value);

                if (sale == null || sale.IsDeleted || sale.Status != "Completed" || sale.CustomerId != transaction.CustomerId)
                {
                    return BadRequest("The linked sale does not exist, is not completed, or belongs to another customer.");
                }

                saleBranchId = sale.BranchId;
            }

            if (!CanModify(await BranchStock.GetScopeAsync(HttpContext, tenantDb), transaction.SalesTransactionId, saleBranchId))
            {
                return NotYourBranch();
            }

            if (customer.LoyaltyPoints + transaction.PointsEarned - transaction.PointsUsed < 0)
            {
                return BadRequest(InputRules.Message($"The customer has only {customer.LoyaltyPoints:N0} points."));
            }

            customer.LoyaltyPoints += transaction.PointsEarned - transaction.PointsUsed;

            tenantDb.LoyaltyTransactions.Add(transaction);
            await tenantDb.SaveChangesAsync();

            return Created(
                $"api/tenant/{companyId}/loyalty/{transaction.LoyaltyTransactionId}",
                transaction);
        }

        [HttpPut("{id:int}")]
        [RequireTenantPermission(TenantPermissions.ManageLoyalty)]
        public async Task<IActionResult> UpdateLoyaltyTransaction(int companyId, int id, LoyaltyTransaction updated)
        {
            string? error = ValidatePoints(updated);
            if (error != null)
            {
                return BadRequest(InputRules.Message(error));
            }

            await using var tenantDb = await _tenantFactory.CreateAsync(companyId);

            var transaction = await tenantDb.LoyaltyTransactions
                .FirstOrDefaultAsync(x => x.LoyaltyTransactionId == id);

            if (transaction == null)
            {
                return NotFound($"LoyaltyTransaction with id {id} not found.");
            }

            if (transaction.IsDeleted)
            {
                return Conflict(InputRules.Message("This loyalty transaction has been deleted and cannot be changed."));
            }

            if (!await CanModifyAsync(tenantDb, transaction))
            {
                return NotYourBranch();
            }

            var customer = await tenantDb.Customers
                .FirstOrDefaultAsync(x => x.CustomerId == transaction.CustomerId);

            if (customer == null)
            {
                return BadRequest("Linked customer no longer exists.");
            }

            int oldDelta = transaction.PointsEarned - transaction.PointsUsed;
            int newDelta = updated.PointsEarned - updated.PointsUsed;

            if (customer.LoyaltyPoints + newDelta - oldDelta < 0)
            {
                return BadRequest(InputRules.Message($"This change would leave the customer with fewer than 0 points (current balance {customer.LoyaltyPoints:N0})."));
            }

            customer.LoyaltyPoints += (newDelta - oldDelta);

            // The date stays as recorded; only the points and the description can be corrected.
            transaction.PointsEarned = updated.PointsEarned;
            transaction.PointsUsed = updated.PointsUsed;
            transaction.TransactionType = updated.TransactionType;

            await tenantDb.SaveChangesAsync();

            return Ok(transaction);
        }

        [HttpDelete("{id:int}")]
        [RequireTenantPermission(TenantPermissions.ManageLoyalty)]
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

            if (!await CanModifyAsync(tenantDb, transaction))
            {
                return NotYourBranch();
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

        // Points are whole, non-negative numbers (at least one of them above 0); the type is a short description.
        private static string? ValidatePoints(LoyaltyTransaction transaction)
        {
            if (transaction.PointsEarned < 0 || transaction.PointsEarned > InputRules.MaxPoints
                || transaction.PointsUsed < 0 || transaction.PointsUsed > InputRules.MaxPoints)
            {
                return $"Points must be between 0 and {InputRules.MaxPoints:N0}.";
            }

            if (transaction.PointsEarned == 0 && transaction.PointsUsed == 0)
            {
                return "Enter the points earned or used.";
            }

            string? error = InputRules.Text(transaction.TransactionType, "Transaction type", 50, true, out var type);
            transaction.TransactionType = type;
            return error;
        }

        // The balance is shared by every branch; each history row gets its branch from the sale that created it.
        public sealed record LoyaltyRow(
            Guid RowGuid,
            DateTime UpdatedAt,
            int LoyaltyTransactionId,
            int CustomerId,
            int? SalesTransactionId,
            int PointsEarned,
            int PointsUsed,
            string TransactionType,
            DateTime Date,
            bool IsDeleted,
            int? BranchId,
            string? BranchName,
            bool CanModify = false);

        private static IQueryable<LoyaltyRow> Project(IQueryable<LoyaltyTransaction> query, TenantCrmDbContext tenantDb)
        {
            return query.Select(x => new LoyaltyRow(
                x.RowGuid,
                x.UpdatedAt,
                x.LoyaltyTransactionId,
                x.CustomerId,
                x.SalesTransactionId,
                x.PointsEarned,
                x.PointsUsed,
                x.TransactionType,
                x.Date,
                x.IsDeleted,
                x.SalesTransaction != null ? x.SalesTransaction.BranchId : null,
                x.SalesTransaction != null
                    ? tenantDb.Branches.Where(b => b.BranchId == x.SalesTransaction.BranchId).Select(b => b.BranchName).FirstOrDefault()
                    : null,
                false));
        }

        // PREMIUM MANAGER: only rows from a sale at their own branch. Manual adjustments (no sale) and other branches'
        // rows are ADMIN-only. ADMIN and non-branching plans: unrestricted. STAFF never reaches these endpoints.
        private static bool CanModify(BranchStock.BranchScope scope, int? salesTransactionId, int? saleBranchId)
        {
            return !scope.Restricted || (salesTransactionId != null && scope.Allows(saleBranchId));
        }

        private async Task<bool> CanModifyAsync(TenantCrmDbContext tenantDb, LoyaltyTransaction transaction)
        {
            var scope = await BranchStock.GetScopeAsync(HttpContext, tenantDb);

            if (!scope.Restricted)
            {
                return true;
            }

            int? saleBranchId = transaction.SalesTransactionId == null
                ? null
                : await tenantDb.SalesTransactions
                    .AsNoTracking()
                    .Where(x => x.TransactionId == transaction.SalesTransactionId.Value)
                    .Select(x => x.BranchId)
                    .FirstOrDefaultAsync();

            return CanModify(scope, transaction.SalesTransactionId, saleBranchId);
        }

        private static ObjectResult NotYourBranch()
        {
            return BranchStock.OtherBranch("Only an administrator can change loyalty records that did not come from a sale at your branch.");
        }
    }
}
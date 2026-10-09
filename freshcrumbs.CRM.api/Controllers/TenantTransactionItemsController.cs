using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using freshcrumbs.CRM.api.Authorization;
using freshcrumbs.CRM.api.Services;
using freshcrumbs.CRM.domain.entities;
using freshcrumbs.CRM.infrastructure.services;

namespace freshcrumbs.CRM.api.Controllers
{
    [ApiController]
    [Route("api/tenant/{companyId:int}/sales/{transactionId:int}/items")]
    public class TenantTransactionItemsController : ControllerBase
    {
        private readonly ITenantDbContextFactory _tenantFactory;

        public TenantTransactionItemsController(ITenantDbContextFactory tenantFactory)
        {
            _tenantFactory = tenantFactory;
        }

        [HttpGet]
        public async Task<IActionResult> GetItems(int companyId, int transactionId)
        {
            await using var tenantDb = await _tenantFactory.CreateAsync(companyId);

            if (!(await BranchStock.GetScopeAsync(HttpContext, tenantDb)).Allows(await GetSaleBranchIdAsync(tenantDb, transactionId)))
            {
                return OtherBranch();
            }

            var items = await tenantDb.TransactionItems
                .AsNoTracking()
                .Where(x => x.TransactionId == transactionId)
                .Select(x => new
                {
                    x.TransactionItemId,
                    x.TransactionId,
                    x.ProductId,
                    x.Quantity,
                    x.UnitPrice,
                    x.Subtotal,
                    Product = x.Product == null ? null : new
                    {
                        x.Product.ProductId,
                        x.Product.ProductCode,
                        x.Product.ProductName,
                        x.Product.Category,
                        x.Product.Price
                    }
                })
                .ToListAsync();

            return Ok(items);
        }

        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetItemById(int companyId, int transactionId, int id)
        {
            await using var tenantDb = await _tenantFactory.CreateAsync(companyId);

            if (!(await BranchStock.GetScopeAsync(HttpContext, tenantDb)).Allows(await GetSaleBranchIdAsync(tenantDb, transactionId)))
            {
                return OtherBranch();
            }

            var item = await tenantDb.TransactionItems
                .AsNoTracking()
                .Where(x => x.TransactionItemId == id && x.TransactionId == transactionId)
                .Select(x => new
                {
                    x.TransactionItemId,
                    x.TransactionId,
                    x.ProductId,
                    x.Quantity,
                    x.UnitPrice,
                    x.Subtotal,
                    Product = x.Product == null ? null : new
                    {
                        x.Product.ProductId,
                        x.Product.ProductCode,
                        x.Product.ProductName,
                        x.Product.Category,
                        x.Product.Price
                    }
                })
                .FirstOrDefaultAsync();

            if (item == null)
            {
                return NotFound($"TransactionItem with id {id} not found for transaction {transactionId}.");
            }

            return Ok(item);
        }

        // Item corrections after a sale is recorded are management actions (STAFF records the whole sale at once).
        [HttpPost]
        [RequireTenantPermission(TenantPermissions.ManageSales)]
        public async Task<IActionResult> AddItem(int companyId, int transactionId, TransactionItem item)
        {
            if (item.Quantity <= 0 || item.Quantity > InputRules.MaxQuantity)
            {
                return BadRequest(InputRules.Message(QuantityMessage));
            }

            await using var tenantDb = await _tenantFactory.CreateAsync(companyId);

            var (sale, saleError) = await GetChangeableSaleAsync(tenantDb, transactionId);

            if (sale == null)
            {
                return saleError!;
            }

            decimal currentTotal = await CurrentTotalAsync(tenantDb, transactionId);

            var saleBranchId = await GetSaleBranchIdAsync(tenantDb, transactionId);

            if (!await BranchStock.CanChangeSaleAsync(HttpContext, tenantDb, saleBranchId))
            {
                return OtherBranch();
            }

            var product = await tenantDb.Products
                .FirstOrDefaultAsync(x => x.ProductId == item.ProductId);

            if (product == null || !string.Equals(product.Status, "Active", StringComparison.OrdinalIgnoreCase))
            {
                return BadRequest(InputRules.Message("The selected product does not exist or is inactive."));
            }

            var stockError = await BranchStock.TakeAsync(tenantDb, product, saleBranchId, item.Quantity);
            if (stockError != null)
            {
                return StockConflict(stockError, saleBranchId);
            }

            // The price comes from the product on the server, never from the client; nested objects are never saved.
            item.TransactionItemId = 0;
            item.RowGuid = Guid.NewGuid();
            item.SalesTransaction = null;
            item.Product = null;
            item.TransactionId = transactionId;
            item.UnitPrice = product.Price;
            item.Subtotal = item.Quantity * item.UnitPrice;

            SetTotals(sale, currentTotal + item.Subtotal);
            tenantDb.TransactionItems.Add(item);
            await tenantDb.SaveChangesAsync();

            var result = new
            {
                item.TransactionItemId,
                item.TransactionId,
                item.ProductId,
                item.Quantity,
                item.UnitPrice,
                item.Subtotal
            };

            return Created(
                $"api/tenant/{companyId}/sales/{transactionId}/items/{item.TransactionItemId}",
                result);
        }

        [HttpPut("{id:int}")]
        [RequireTenantPermission(TenantPermissions.ManageSales)]
        public async Task<IActionResult> UpdateItem(int companyId, int transactionId, int id, TransactionItem updated)
        {
            if (updated.Quantity <= 0 || updated.Quantity > InputRules.MaxQuantity)
            {
                return BadRequest(InputRules.Message(QuantityMessage));
            }

            await using var tenantDb = await _tenantFactory.CreateAsync(companyId);

            var item = await tenantDb.TransactionItems
                .FirstOrDefaultAsync(x => x.TransactionItemId == id && x.TransactionId == transactionId);

            if (item == null)
            {
                return NotFound($"TransactionItem with id {id} not found for transaction {transactionId}.");
            }

            var (sale, saleError) = await GetChangeableSaleAsync(tenantDb, transactionId);

            if (sale == null)
            {
                return saleError!;
            }

            decimal currentTotal = await CurrentTotalAsync(tenantDb, transactionId);

            var saleBranchId = await GetSaleBranchIdAsync(tenantDb, transactionId);

            if (!await BranchStock.CanChangeSaleAsync(HttpContext, tenantDb, saleBranchId))
            {
                return OtherBranch();
            }

            var product = await tenantDb.Products
                .FirstOrDefaultAsync(x => x.ProductId == updated.ProductId);

            if (product == null
                || (updated.ProductId != item.ProductId && !string.Equals(product.Status, "Active", StringComparison.OrdinalIgnoreCase)))
            {
                return BadRequest(InputRules.Message("The selected product does not exist or is inactive."));
            }

            if (item.ProductId == updated.ProductId)
            {
                int quantityDelta = updated.Quantity - item.Quantity;

                if (quantityDelta > 0)
                {
                    var stockError = await BranchStock.TakeAsync(tenantDb, product, saleBranchId, quantityDelta);
                    if (stockError != null)
                    {
                        return StockConflict(stockError, saleBranchId);
                    }
                }
                else
                {
                    await BranchStock.ReturnAsync(tenantDb, product, saleBranchId, -quantityDelta);
                }
            }
            else
            {
                var oldProduct = await tenantDb.Products
                    .FirstOrDefaultAsync(x => x.ProductId == item.ProductId);

                if (oldProduct != null)
                {
                    await BranchStock.ReturnAsync(tenantDb, oldProduct, saleBranchId, item.Quantity);
                }

                var stockError = await BranchStock.TakeAsync(tenantDb, product, saleBranchId, updated.Quantity);
                if (stockError != null)
                {
                    return StockConflict(stockError, saleBranchId);
                }
            }

            decimal oldSubtotal = item.Subtotal;

            item.ProductId = updated.ProductId;
            item.Quantity = updated.Quantity;
            item.UnitPrice = product.Price;
            item.Subtotal = updated.Quantity * product.Price;

            SetTotals(sale, currentTotal - oldSubtotal + item.Subtotal);

            await tenantDb.SaveChangesAsync();

            var result = new
            {
                item.TransactionItemId,
                item.TransactionId,
                item.ProductId,
                item.Quantity,
                item.UnitPrice,
                item.Subtotal
            };

            return Ok(result);
        }

        [HttpDelete("{id:int}")]
        [RequireTenantPermission(TenantPermissions.ManageSales)]
        public async Task<IActionResult> DeleteItem(int companyId, int transactionId, int id)
        {
            await using var tenantDb = await _tenantFactory.CreateAsync(companyId);

            var item = await tenantDb.TransactionItems
                .FirstOrDefaultAsync(x => x.TransactionItemId == id && x.TransactionId == transactionId);

            if (item == null)
            {
                return NotFound($"TransactionItem with id {id} not found for transaction {transactionId}.");
            }

            var (sale, saleError) = await GetChangeableSaleAsync(tenantDb, transactionId);

            if (sale == null)
            {
                return saleError!;
            }

            decimal currentTotal = await CurrentTotalAsync(tenantDb, transactionId);

            var saleBranchId = await GetSaleBranchIdAsync(tenantDb, transactionId);

            if (!await BranchStock.CanChangeSaleAsync(HttpContext, tenantDb, saleBranchId))
            {
                return OtherBranch();
            }

            var product = await tenantDb.Products
                .FirstOrDefaultAsync(x => x.ProductId == item.ProductId);

            if (product != null)
            {
                await BranchStock.ReturnAsync(tenantDb, product, saleBranchId, item.Quantity);
            }

            SetTotals(sale, currentTotal - item.Subtotal);
            tenantDb.TransactionItems.Remove(item);
            await tenantDb.SaveChangesAsync();

            return NoContent();
        }

        private const string QuantityMessage = "Quantity must be greater than zero and at most 99,999.";

        // Item corrections keep the sale's total consistent on the server: the sale must exist and be open, and its
        // amount may not depend on a promotion, a Senior/PWD discount or loyalty points (those are only calculated when
        // the whole sale is recorded). Such a sale is corrected by cancelling it and recording it again.
        private async Task<(SalesTransaction? Sale, IActionResult? Error)> GetChangeableSaleAsync(
            freshcrumbs.CRM.infrastructure.data.TenantCrmDbContext tenantDb,
            int transactionId)
        {
            var sale = await tenantDb.SalesTransactions.FirstOrDefaultAsync(x => x.TransactionId == transactionId);

            if (sale == null)
            {
                return (null, NotFound(InputRules.Message("The selected sale does not exist.")));
            }

            if (sale.IsDeleted || string.Equals(sale.Status, "Cancelled", StringComparison.OrdinalIgnoreCase))
            {
                return (null, Conflict("Items cannot be changed because this transaction is cancelled or deleted."));
            }

            bool customerDiscount = await tenantDb.CustomerDiscountEligibilities
                .AnyAsync(e => e.CustomerId == sale.CustomerId && e.VerificationStatus == "Verified");

            if (sale.PromotionId != null || sale.DiscountAmount != 0 || sale.CustomerDiscountAmount != 0
                || sale.PointsUsed != 0 || sale.PointsEarned != 0
                || customerDiscount || HttpContext.HasTenantPermission(TenantPermissions.UseLoyalty))
            {
                return (null, Conflict(InputRules.Message(
                    "This sale has a discount or loyalty points, so its items cannot be changed one by one. Cancel it and record the sale again.")));
            }

            return (sale, null);
        }

        private static async Task<decimal> CurrentTotalAsync(
            freshcrumbs.CRM.infrastructure.data.TenantCrmDbContext tenantDb,
            int transactionId)
        {
            return await tenantDb.TransactionItems.AsNoTracking()
                .Where(x => x.TransactionId == transactionId)
                .SumAsync(x => (decimal?)x.Subtotal) ?? 0m;
        }

        // No discounts or points on these sales (checked above), so the final amount equals the total.
        private static void SetTotals(SalesTransaction sale, decimal total)
        {
            sale.TotalAmount = total;
            sale.FinalAmount = total;
        }

        private static async Task<int?> GetSaleBranchIdAsync(
            freshcrumbs.CRM.infrastructure.data.TenantCrmDbContext tenantDb,
            int transactionId)
        {
            return await tenantDb.SalesTransactions.AsNoTracking()
                .Where(x => x.TransactionId == transactionId)
                .Select(x => x.BranchId)
                .FirstOrDefaultAsync();
        }

        // Non-branch sales keep the original plain-text response; branch sales return { message } for the UI.
        private ObjectResult StockConflict(string message, int? saleBranchId)
        {
            return saleBranchId == null ? Conflict(message) : Conflict(new { message });
        }

        private ObjectResult OtherBranch()
        {
            return StatusCode(StatusCodes.Status403Forbidden, new
            {
                code = "OtherBranch",
                message = "This sale belongs to another branch."
            });
        }
    }
}
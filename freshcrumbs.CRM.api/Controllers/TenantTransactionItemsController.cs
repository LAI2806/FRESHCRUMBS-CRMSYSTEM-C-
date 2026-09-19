using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
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

        [HttpPost]
        public async Task<IActionResult> AddItem(int companyId, int transactionId, TransactionItem item)
        {
            await using var tenantDb = await _tenantFactory.CreateAsync(companyId);

            var transactionExists = await tenantDb.SalesTransactions
                .AnyAsync(x => x.TransactionId == transactionId);

            if (!transactionExists)
            {
                return BadRequest($"TransactionId {transactionId} does not exist for this tenant.");
            }

            var product = await tenantDb.Products
                .FirstOrDefaultAsync(x => x.ProductId == item.ProductId);

            if (product == null)
            {
                return BadRequest($"ProductId {item.ProductId} does not exist for this tenant.");
            }

            if (product.Quantity < item.Quantity)
            {
                return Conflict($"Insufficient stock for {product.ProductName}. Available: {product.Quantity}.");
            }

            item.TransactionId = transactionId;
            item.Subtotal = item.Quantity * item.UnitPrice;

            product.Quantity -= item.Quantity;

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
        [HttpPut("{id:int}")]
        public async Task<IActionResult> UpdateItem(int companyId, int transactionId, int id, TransactionItem updated)
        {
            await using var tenantDb = await _tenantFactory.CreateAsync(companyId);

            var item = await tenantDb.TransactionItems
                .FirstOrDefaultAsync(x => x.TransactionItemId == id && x.TransactionId == transactionId);

            if (item == null)
            {
                return NotFound($"TransactionItem with id {id} not found for transaction {transactionId}.");
            }

            var product = await tenantDb.Products
                .FirstOrDefaultAsync(x => x.ProductId == updated.ProductId);

            if (product == null)
            {
                return BadRequest($"ProductId {updated.ProductId} does not exist for this tenant.");
            }

            if (item.ProductId == updated.ProductId)
            {
                int quantityDelta = updated.Quantity - item.Quantity;

                if (quantityDelta > 0 && product.Quantity < quantityDelta)
                {
                    return Conflict($"Insufficient stock for {product.ProductName}. Available: {product.Quantity}.");
                }

                product.Quantity -= quantityDelta;
            }
            else
            {
                var oldProduct = await tenantDb.Products
                    .FirstOrDefaultAsync(x => x.ProductId == item.ProductId);

                if (oldProduct != null)
                {
                    oldProduct.Quantity += item.Quantity;
                }

                if (product.Quantity < updated.Quantity)
                {
                    return Conflict($"Insufficient stock for {product.ProductName}. Available: {product.Quantity}.");
                }

                product.Quantity -= updated.Quantity;
            }

            item.ProductId = updated.ProductId;
            item.Quantity = updated.Quantity;
            item.UnitPrice = updated.UnitPrice;
            item.Subtotal = updated.Quantity * updated.UnitPrice;

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
        public async Task<IActionResult> DeleteItem(int companyId, int transactionId, int id)
        {
            await using var tenantDb = await _tenantFactory.CreateAsync(companyId);

            var item = await tenantDb.TransactionItems
                .FirstOrDefaultAsync(x => x.TransactionItemId == id && x.TransactionId == transactionId);

            if (item == null)
            {
                return NotFound($"TransactionItem with id {id} not found for transaction {transactionId}.");
            }

            var product = await tenantDb.Products
                .FirstOrDefaultAsync(x => x.ProductId == item.ProductId);

            if (product != null)
            {
                product.Quantity += item.Quantity;
            }

            tenantDb.TransactionItems.Remove(item);
            await tenantDb.SaveChangesAsync();

            return NoContent();
        }
    }
}
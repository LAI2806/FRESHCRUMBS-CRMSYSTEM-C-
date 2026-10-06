using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using freshcrumbs.CRM.domain.entities;
using freshcrumbs.CRM.infrastructure.services;

namespace freshcrumbs.CRM.api.Controllers
{
    [ApiController]
    [Route("api/tenant/{companyId:int}/products")]
    public class TenantProductsController : ControllerBase
    {
        private readonly ITenantDbContextFactory _tenantFactory;

        public TenantProductsController(ITenantDbContextFactory tenantFactory)
        {
            _tenantFactory = tenantFactory;
        }

        [HttpGet]
        public async Task<IActionResult> GetProducts(int companyId, bool includeInactive = false)
        {
            await using var tenantDb = await _tenantFactory.CreateAsync(companyId);

            var query = tenantDb.Products.AsNoTracking();

            if (!includeInactive)
            {
                query = query.Where(x => x.Status == "Active");
            }

            var products = await query
                .OrderBy(x => x.ProductId)
                .ToListAsync();

            var soldByProduct = await tenantDb.TransactionItems
                .AsNoTracking()
                .Where(i => i.SalesTransaction != null
                    && i.SalesTransaction.Status == "Completed"
                    && !i.SalesTransaction.IsDeleted)
                .GroupBy(i => i.ProductId)
                .Select(g => new { ProductId = g.Key, Sold = g.Sum(i => i.Quantity) })
                .ToListAsync();

            foreach (var product in products)
            {
                product.Sold = soldByProduct
                    .FirstOrDefault(s => s.ProductId == product.ProductId)?.Sold ?? 0;
            }

            return Ok(products);
        }

        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetProductById(int companyId, int id)
        {
            await using var tenantDb = await _tenantFactory.CreateAsync(companyId);

            var product = await tenantDb.Products
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.ProductId == id);

            if (product == null)
            {
                return NotFound($"Product with id {id} not found.");
            }

            product.Sold = await tenantDb.TransactionItems
                .AsNoTracking()
                .Where(i => i.ProductId == id
                    && i.SalesTransaction != null
                    && i.SalesTransaction.Status == "Completed"
                    && !i.SalesTransaction.IsDeleted)
                .SumAsync(i => i.Quantity);

            return Ok(product);
        }

        [HttpPost]
        public async Task<IActionResult> CreateProduct(int companyId, Product product)
        {
            if (product.Quantity < 0)
            {
                return BadRequest("Stock cannot be negative.");
            }

            if (product.ReorderLevel < 0)
            {
                return BadRequest("Reorder level cannot be negative.");
            }

            await using var tenantDb = await _tenantFactory.CreateAsync(companyId);

            tenantDb.Products.Add(product);
            await tenantDb.SaveChangesAsync();

            return Created(
                $"api/tenant/{companyId}/products/{product.ProductId}",
                product);
        }

        [HttpPut("{id:int}")]
        public async Task<IActionResult> UpdateProduct(int companyId, int id, Product updated)
        {
            if (updated.Quantity < 0)
            {
                return BadRequest("Stock cannot be negative.");
            }

            if (updated.ReorderLevel < 0)
            {
                return BadRequest("Reorder level cannot be negative.");
            }

            await using var tenantDb = await _tenantFactory.CreateAsync(companyId);

            var product = await tenantDb.Products.FirstOrDefaultAsync(x => x.ProductId == id);

            if (product == null)
            {
                return NotFound($"Product with id {id} not found.");
            }

            product.ProductCode = updated.ProductCode;
            product.ProductName = updated.ProductName;
            product.Category = updated.Category;
            product.Description = updated.Description;
            product.Price = updated.Price;
            product.Quantity = updated.Quantity;
            product.ReorderLevel = updated.ReorderLevel;
            product.Status = updated.Status;

            await tenantDb.SaveChangesAsync();

            return Ok(product);
        }

        [HttpDelete("{id:int}")]
        public async Task<IActionResult> DeleteProduct(int companyId, int id)
        {
            await using var tenantDb = await _tenantFactory.CreateAsync(companyId);

            var product = await tenantDb.Products.FirstOrDefaultAsync(x => x.ProductId == id);

            if (product == null)
            {
                return NotFound($"Product with id {id} not found.");
            }

            product.Status = "Inactive";
            await tenantDb.SaveChangesAsync();

            return NoContent();
        }

        [HttpPut("{id:int}/reactivate")]
        public async Task<IActionResult> ReactivateProduct(int companyId, int id)
        {
            await using var tenantDb = await _tenantFactory.CreateAsync(companyId);

            var product = await tenantDb.Products.FirstOrDefaultAsync(x => x.ProductId == id);

            if (product == null)
            {
                return NotFound($"Product with id {id} not found.");
            }

            product.Status = "Active";
            await tenantDb.SaveChangesAsync();

            return Ok(product);
        }
    }
}
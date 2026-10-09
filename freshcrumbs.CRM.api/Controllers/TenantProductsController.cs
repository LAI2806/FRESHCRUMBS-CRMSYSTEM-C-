using freshcrumbs.CRM.api.Authorization;
using freshcrumbs.CRM.api.Services;
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
        [RequireTenantPermission(TenantPermissions.ViewProducts)]
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

            // Branching plans: Quantity is the company total; BranchQuantity is the caller's own branch stock.
            if (BranchStock.IsBranchingCompany(HttpContext))
            {
                var assigned = await BranchStock.GetAssignedBranchAsync(tenantDb, User);

                if (assigned != null)
                {
                    var branchStock = await tenantDb.BranchInventories.AsNoTracking()
                        .Where(x => x.BranchId == assigned.BranchId)
                        .ToDictionaryAsync(x => x.ProductId, x => x.Quantity);

                    foreach (var product in products)
                    {
                        product.BranchQuantity = branchStock.TryGetValue(product.ProductId, out var quantity) ? quantity : 0;
                    }
                }
            }

            return Ok(products);
        }

        [HttpGet("{id:int}")]
        [RequireTenantPermission(TenantPermissions.ViewProducts)]
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
        [RequireTenantPermission(TenantPermissions.ManageProducts)]
        public async Task<IActionResult> CreateProduct(int companyId, Product product)
        {
            string? error = ValidateProduct(product, null);

            if (error != null)
            {
                return BadRequest(InputRules.Message(error));
            }

            // Identity and links are set here, never taken from the client.
            product.ProductId = 0;
            product.RowGuid = Guid.NewGuid();
            product.CreatedAt = DateTime.UtcNow;
            product.Status = "Active";
            product.TransactionItems = new List<TransactionItem>();

            if (BranchStock.IsBranchingCompany(HttpContext) && product.Quantity != 0)
            {
                return BadRequest(new { message = "On a branch plan, new products start with 0 stock. Add stock per branch (Branches > Stock In)." });
            }

            await using var tenantDb = await _tenantFactory.CreateAsync(companyId);

            if (await tenantDb.Products.AnyAsync(x => x.ProductCode == product.ProductCode))
            {
                return DuplicateCode(product.ProductCode);
            }

            tenantDb.Products.Add(product);

            try
            {
                await tenantDb.SaveChangesAsync();
            }
            catch (DbUpdateException ex) when (ex.InnerException?.Message.Contains("ProductCode") == true)
            {
                return DuplicateCode(product.ProductCode);
            }

            return Created(
                $"api/tenant/{companyId}/products/{product.ProductId}",
                product);
        }

        [HttpPut("{id:int}")]
        [RequireTenantPermission(TenantPermissions.ManageProducts)]
        public async Task<IActionResult> UpdateProduct(int companyId, int id, Product updated)
        {
            string? status = InputRules.OneOf(updated.Status, "Active", "Inactive");

            if (status == null)
            {
                return BadRequest(InputRules.Message("Status must be Active or Inactive."));
            }

            await using var tenantDb = await _tenantFactory.CreateAsync(companyId);

            var product = await tenantDb.Products.FirstOrDefaultAsync(x => x.ProductId == id);

            if (product == null)
            {
                return NotFound($"Product with id {id} not found.");
            }

            // Branch plans: the total stock is never edited here, so it is not checked either.
            if (BranchStock.IsBranchingCompany(HttpContext))
            {
                updated.Quantity = product.Quantity;
            }

            string? error = ValidateProduct(updated, product);

            if (error != null)
            {
                return BadRequest(InputRules.Message(error));
            }

            if (await tenantDb.Products.AnyAsync(x => x.ProductId != id && x.ProductCode == updated.ProductCode))
            {
                return DuplicateCode(updated.ProductCode);
            }

            product.ProductCode = updated.ProductCode;
            product.ProductName = updated.ProductName;
            product.Category = updated.Category;
            product.Description = updated.Description;
            product.Price = updated.Price;
            // Branching plans: the total follows branch stock, so it is never overwritten from this form.
            if (!BranchStock.IsBranchingCompany(HttpContext))
            {
                product.Quantity = updated.Quantity;
            }
            product.ReorderLevel = updated.ReorderLevel;
            product.Status = status;

            try
            {
                await tenantDb.SaveChangesAsync();
            }
            catch (DbUpdateException ex) when (ex.InnerException?.Message.Contains("ProductCode") == true)
            {
                return DuplicateCode(updated.ProductCode);
            }

            return Ok(product);
        }

        [HttpDelete("{id:int}")]
        [RequireTenantPermission(TenantPermissions.ManageProducts)]
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
        [RequireTenantPermission(TenantPermissions.ManageProducts)]
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

        // Field rules (database limits; text is trimmed, otherwise free so real bakery names are accepted).
        // existing = the stored product on an edit: an older price or stock value is accepted while it is unchanged.
        private static string? ValidateProduct(Product product, Product? existing)
        {
            string? codeError = InputRules.Text(product.ProductCode, "Product code", 50, true, out var code);
            string? nameError = InputRules.Text(product.ProductName, "Product name", 200, true, out var name);
            string? categoryError = InputRules.Text(product.Category, "Category", 100, true, out var category);
            string? descriptionError = InputRules.Text(product.Description, "Description", 500, false, out var description);
            string? error = codeError ?? nameError ?? categoryError ?? descriptionError;

            if (error != null)
            {
                return error;
            }

            product.ProductCode = code;
            product.ProductName = name;
            product.Category = category;
            product.Description = description;

            bool priceUnchanged = existing != null && product.Price == existing.Price;

            if (!priceUnchanged && !InputRules.IsMoney(product.Price, 0.01m))
            {
                return $"Price must be between 0.01 and {InputRules.MaxMoney:N2} with at most 2 decimal places.";
            }

            bool stockUnchanged = existing != null && product.Quantity == existing.Quantity;

            if (!stockUnchanged && (product.Quantity < 0 || product.Quantity > InputRules.MaxStock))
            {
                return $"Stock must be between 0 and {InputRules.MaxStock:N0}.";
            }

            if (product.ReorderLevel < 0 || product.ReorderLevel > InputRules.MaxStock)
            {
                return $"Reorder level must be between 0 and {InputRules.MaxStock:N0}.";
            }

            return null;
        }

        private ObjectResult DuplicateCode(string productCode)
        {
            return Conflict(new
            {
                code = "DuplicateProductCode",
                message = $"Product code \"{productCode}\" is already used by another product."
            });
        }
    }
}
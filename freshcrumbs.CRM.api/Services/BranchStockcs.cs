using System.Security.Claims;
using freshcrumbs.CRM.api.Authorization;
using freshcrumbs.CRM.domain.entities;
using freshcrumbs.CRM.infrastructure.data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace freshcrumbs.CRM.api.Services
{
    // Stock rules shared by sales, sale items and branch inventory.
    //   Non-branch plans (Basic / Standard): Product.Quantity is the stock.
    //   Branching plans (Premium): BranchInventory is the stock of each branch and Product.Quantity is the
    //   company-wide total, so every branch change moves both by the same amount in the same save.
    public static class BranchStock
    {
        public static bool IsBranchingCompany(HttpContext context)
        {
            return context.GetTenantFeatures().Contains(PlanFeatureKeys.Branching, StringComparer.OrdinalIgnoreCase);
        }

        public static bool IsAdmin(HttpContext context)
        {
            return string.Equals(context.GetTenantRole(), TenantRoles.Admin, StringComparison.Ordinal);
        }

        // The caller's branch, only when it is assigned to an ACTIVE branch.
        public static async Task<Branch?> GetAssignedBranchAsync(TenantCrmDbContext db, ClaimsPrincipal user)
        {
            var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);

            if (string.IsNullOrEmpty(userId))
            {
                return null;
            }

            return await db.BranchAssignments.AsNoTracking()
                .Where(a => a.UserId == userId && a.Branch != null && a.Branch.Status == "Active")
                .Select(a => a.Branch)
                .FirstOrDefaultAsync();
        }

        // Which branch data the caller may access (BRANCH, separate from ROLE and SUBSCRIPTION):
        //   non-branching plans and ADMIN: everything (Restricted = false);
        //   Branching plans, MANAGER / STAFF: only their assigned ACTIVE branch, taken from BranchAssignment,
        //   never from the request. Restricted with BranchId = null (not assigned) allows nothing.
        public sealed record BranchScope(bool Restricted, int? BranchId)
        {
            public bool Allows(int? recordBranchId) => !Restricted || (BranchId != null && recordBranchId == BranchId);
        }

        public static async Task<BranchScope> GetScopeAsync(HttpContext context, TenantCrmDbContext db)
        {
            if (!IsBranchingCompany(context) || IsAdmin(context))
            {
                return new BranchScope(false, null);
            }

            var assigned = await GetAssignedBranchAsync(db, context.User);
            return new BranchScope(true, assigned?.BranchId);
        }

        // Branch recorded on new Customer / Feedback / Inquiry rows: never the client's value.
        // Restricted users get their own branch (null when not assigned: the caller refuses the action);
        // ADMIN gets their assigned branch if any; non-branching plans store no branch.
        public static async Task<int?> GetRecordingBranchIdAsync(HttpContext context, TenantCrmDbContext db)
        {
            if (!IsBranchingCompany(context))
            {
                return null;
            }

            return (await GetAssignedBranchAsync(db, context.User))?.BranchId;
        }

        public static Task<Dictionary<int, string>> BranchNamesAsync(TenantCrmDbContext db)
        {
            return db.Branches.AsNoTracking().ToDictionaryAsync(b => b.BranchId, b => b.BranchName);
        }

        public static ObjectResult OtherBranch(string message)
        {
            return new ObjectResult(new { code = "OtherBranch", message }) { StatusCode = StatusCodes.Status403Forbidden };
        }

        public static ObjectResult NoBranchAssigned()
        {
            return new ObjectResult(new
            {
                code = "NoBranchAssigned",
                message = "You are not assigned to a branch. Ask your administrator to assign you to a branch."
            })
            { StatusCode = StatusCodes.Status403Forbidden };
        }

        // A customer is associated with a branch through registration or completed sales there (not ownership).
        public static IQueryable<Customer> AssociatedWith(IQueryable<Customer> customers, TenantCrmDbContext db, int branchId)
        {
            return customers.Where(c => c.BranchId == branchId
                || db.SalesTransactions.Any(s => s.CustomerId == c.CustomerId && s.BranchId == branchId && !s.IsDeleted && s.Status == "Completed"));
        }

        // A MANAGER / STAFF may only change sales of their own branch. Sales recorded before branching (no branch) are ADMIN-only on Branching plans.
        public static async Task<bool> CanChangeSaleAsync(HttpContext context, TenantCrmDbContext db, int? saleBranchId)
        {
            return (await GetScopeAsync(context, db)).Allows(saleBranchId);
        }

        // Removes stock for a sale. Returns an error message (nothing changed) or null.
        public static async Task<string?> TakeAsync(TenantCrmDbContext db, Product product, int? branchId, int quantity)
        {
            // Non-branch stock keeps the original behaviour exactly.
            if (branchId == null)
            {
                if (product.Quantity < quantity)
                {
                    return $"Insufficient stock for {product.ProductName}. Available: {product.Quantity}.";
                }

                product.Quantity -= quantity;
                return null;
            }

            if (quantity <= 0)
            {
                return null;
            }

            var inventory = await FindAsync(db, branchId.Value, product.ProductId);

            int available = inventory?.Quantity ?? 0;

            if (inventory == null || available < quantity)
            {
                return $"Insufficient stock for {product.ProductName} at this branch. Available: {available}.";
            }

            inventory.Quantity -= quantity;
            product.Quantity -= quantity;
            return null;
        }

        // Includes rows added earlier in the same unit of work (not in the database yet).
        public static async Task<BranchInventory?> FindAsync(TenantCrmDbContext db, int branchId, int productId)
        {
            return db.BranchInventories.Local.FirstOrDefault(x => x.BranchId == branchId && x.ProductId == productId)
                ?? await db.BranchInventories.FirstOrDefaultAsync(x => x.BranchId == branchId && x.ProductId == productId);
        }

        // Puts stock back (removed item, smaller quantity, cancelled sale).
        public static async Task ReturnAsync(TenantCrmDbContext db, Product product, int? branchId, int quantity)
        {
            if (branchId == null)
            {
                product.Quantity += quantity;
                return;
            }

            if (quantity <= 0)
            {
                return;
            }

            product.Quantity += quantity;

            var inventory = await FindAsync(db, branchId.Value, product.ProductId);

            if (inventory == null)
            {
                db.BranchInventories.Add(new BranchInventory
                {
                    BranchId = branchId.Value,
                    ProductId = product.ProductId,
                    Quantity = quantity
                });
            }
            else
            {
                inventory.Quantity += quantity;
            }
        }
    }
}

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
    [Route("api/tenant/{companyId:int}/promotions")]
    public class TenantPromotionsController : ControllerBase
    {
        private readonly ITenantDbContextFactory _tenantFactory;

        public TenantPromotionsController(ITenantDbContextFactory tenantFactory)
        {
            _tenantFactory = tenantFactory;
        }

        [HttpGet]
        [RequireTenantPermission(TenantPermissions.UsePromotions)]
        public async Task<IActionResult> GetPromotions(int companyId, bool includeInactive = false)
        {
            if (!HttpContext.HasTenantPermission(TenantPermissions.ManagePromotions))
            {
                includeInactive = false;
            }

            await using var tenantDb = await _tenantFactory.CreateAsync(companyId);

            var query = tenantDb.Promotions.AsNoTracking();

            if (!includeInactive)
            {
                query = query.Where(x => x.Status == "Active");
            }

            var promotions = await query
                .OrderBy(x => x.PromotionId)
                .ToListAsync();

            var access = await GetAccessAsync(tenantDb);
            var names = await BranchStock.BranchNamesAsync(tenantDb);

            return Ok(promotions.Where(access.CanSee).Select(p => ToResponse(p, access, names)));
        }

        [HttpGet("{id:int}")]
        [RequireTenantPermission(TenantPermissions.UsePromotions)]
        public async Task<IActionResult> GetPromotionById(int companyId, int id)
        {
            await using var tenantDb = await _tenantFactory.CreateAsync(companyId);

            var promotion = await tenantDb.Promotions
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.PromotionId == id);

            var access = await GetAccessAsync(tenantDb);

            if (promotion == null || !access.CanSee(promotion))
            {
                return NotFound($"Promotion with id {id} not found.");
            }

            return Ok(ToResponse(promotion, access, await BranchStock.BranchNamesAsync(tenantDb)));
        }

        [HttpPost]
        [RequireTenantPermission(TenantPermissions.ManagePromotions)]
        public async Task<IActionResult> CreatePromotion(int companyId, Promotion promotion)
        {
            string? error = ValidatePromotion(promotion);
            if (error != null)
            {
                return BadRequest(InputRules.Message(error));
            }

            await using var tenantDb = await _tenantFactory.CreateAsync(companyId);

            promotion.PromotionId = 0;
            promotion.RowGuid = Guid.NewGuid();
            promotion.Status = "Active";

            var access = await GetAccessAsync(tenantDb);
            var branch = await ResolveBranchAsync(tenantDb, access, promotion.BranchId, null);

            if (branch.Error != null)
            {
                return branch.Error;
            }

            promotion.BranchId = branch.BranchId;

            tenantDb.Promotions.Add(promotion);
            await tenantDb.SaveChangesAsync();

            return Created(
                $"api/tenant/{companyId}/promotions/{promotion.PromotionId}",
                promotion);
        }

        [HttpPut("{id:int}")]
        [RequireTenantPermission(TenantPermissions.ManagePromotions)]
        public async Task<IActionResult> UpdatePromotion(int companyId, int id, Promotion updated)
        {
            string? error = ValidatePromotion(updated);
            if (error != null)
            {
                return BadRequest(InputRules.Message(error));
            }

            string? status = InputRules.OneOf(updated.Status, "Active", "Inactive");
            if (status == null)
            {
                return BadRequest(InputRules.Message("Status must be Active or Inactive."));
            }

            await using var tenantDb = await _tenantFactory.CreateAsync(companyId);

            var promotion = await tenantDb.Promotions.FirstOrDefaultAsync(x => x.PromotionId == id);

            if (promotion == null)
            {
                return NotFound($"Promotion with id {id} not found.");
            }

            var access = await GetAccessAsync(tenantDb);

            if (!access.CanManage(promotion))
            {
                return NotYours();
            }

            var branch = await ResolveBranchAsync(tenantDb, access, updated.BranchId, promotion);

            if (branch.Error != null)
            {
                return branch.Error;
            }

            promotion.BranchId = branch.BranchId;
            promotion.PromotionName = updated.PromotionName;
            promotion.Description = updated.Description;
            promotion.DiscountType = updated.DiscountType;
            promotion.DiscountValue = updated.DiscountValue;
            promotion.MinimumPurchase = updated.MinimumPurchase;
            promotion.RequiredLoyaltyPoints = updated.RequiredLoyaltyPoints;
            promotion.EligibilityCategory = updated.EligibilityCategory;
            promotion.StartDate = updated.StartDate;
            promotion.EndDate = updated.EndDate;
            promotion.Status = status;

            await tenantDb.SaveChangesAsync();

            return Ok(promotion);
        }

        [HttpDelete("{id:int}")]
        [RequireTenantPermission(TenantPermissions.ManagePromotions)]
        public async Task<IActionResult> DeletePromotion(int companyId, int id)
        {
            await using var tenantDb = await _tenantFactory.CreateAsync(companyId);

            var promotion = await tenantDb.Promotions.FirstOrDefaultAsync(x => x.PromotionId == id);

            if (promotion == null)
            {
                return NotFound($"Promotion with id {id} not found.");
            }

            if (!(await GetAccessAsync(tenantDb)).CanManage(promotion))
            {
                return NotYours();
            }

            promotion.Status = "Inactive";
            await tenantDb.SaveChangesAsync();

            return NoContent();
        }

        [HttpPut("{id:int}/reactivate")]
        [RequireTenantPermission(TenantPermissions.ManagePromotions)]
        public async Task<IActionResult> ReactivatePromotion(int companyId, int id)
        {
            await using var tenantDb = await _tenantFactory.CreateAsync(companyId);

            var promotion = await tenantDb.Promotions.FirstOrDefaultAsync(x => x.PromotionId == id);

            if (promotion == null)
            {
                return NotFound($"Promotion with id {id} not found.");
            }

            if (!(await GetAccessAsync(tenantDb)).CanManage(promotion))
            {
                return NotYours();
            }

            promotion.Status = "Active";
            await tenantDb.SaveChangesAsync();

            return Ok(promotion);
        }

        // Promotion scope (BranchId null = company-wide, a value = that branch only):
        //   PREMIUM ADMIN: sees and manages every promotion.
        //   PREMIUM MANAGER: sees company-wide + own branch; manages only own-branch promotions (branch from BranchAssignment).
        //   PREMIUM STAFF: sees company-wide + own branch (for sales); manages nothing.
        //   Plans without branches: company-wide promotions as before; a stored branch promotion (left over from
        //   PREMIUM) is never applied in sales and only an ADMIN sees or manages it. Its BranchId is never rewritten.
        private sealed record PromotionAccess(bool Branching, bool Admin, BranchStock.BranchScope Scope, bool Manager)
        {
            public bool CanSee(Promotion p) => Branching
                ? !Scope.Restricted || p.BranchId == null || p.BranchId == Scope.BranchId
                : p.BranchId == null || Admin;

            public bool CanManage(Promotion p) => Manager && (Branching
                ? !Scope.Restricted || (Scope.BranchId != null && p.BranchId == Scope.BranchId)
                : p.BranchId == null || Admin);
        }

        private async Task<PromotionAccess> GetAccessAsync(TenantCrmDbContext tenantDb)
        {
            return new PromotionAccess(
                BranchStock.IsBranchingCompany(HttpContext),
                BranchStock.IsAdmin(HttpContext),
                await BranchStock.GetScopeAsync(HttpContext, tenantDb),
                HttpContext.HasTenantPermission(TenantPermissions.ManagePromotions));
        }

        // Branch to store: PREMIUM ADMIN may choose (null = company-wide) any active branch; a PREMIUM MANAGER always gets
        // their assigned branch; on plans without branches a new promotion is company-wide and an existing one keeps its value.
        private async Task<(int? BranchId, IActionResult? Error)> ResolveBranchAsync(
            TenantCrmDbContext tenantDb, PromotionAccess access, int? requested, Promotion? existing)
        {
            if (!access.Branching)
            {
                return (existing?.BranchId, null);
            }

            if (access.Scope.Restricted)
            {
                if (access.Scope.BranchId == null)
                {
                    return (null, BranchStock.NoBranchAssigned());
                }

                return (access.Scope.BranchId, null);
            }

            if (requested == null || requested == existing?.BranchId)
            {
                return (requested, null);
            }

            if (!await tenantDb.Branches.AnyAsync(b => b.BranchId == requested && b.Status == "Active"))
            {
                return (null, BadRequest(new { message = "The selected branch does not exist or is inactive." }));
            }

            return (requested, null);
        }

        // Field rules (database limits and the promotion calculation used in sales).
        private static string? ValidatePromotion(Promotion promotion)
        {
            string? nameError = InputRules.Text(promotion.PromotionName, "Promotion name", 200, true, out var name);
            string? descriptionError = InputRules.Text(promotion.Description, "Description", 500, false, out var description);
            string? error = nameError ?? descriptionError;

            if (error != null)
            {
                return error;
            }

            promotion.PromotionName = name;
            promotion.Description = description;

            string? discountType = InputRules.OneOf(promotion.DiscountType, "Percentage", "Fixed Amount");
            if (discountType == null)
            {
                return "Discount type must be Percentage or Fixed Amount.";
            }

            promotion.DiscountType = discountType;

            if (!InputRules.IsMoney(promotion.DiscountValue, 0.01m))
            {
                return "Discount value must be greater than zero with at most 2 decimal places.";
            }

            if (discountType == "Percentage" && promotion.DiscountValue > 100)
            {
                return "A percentage discount cannot be more than 100%.";
            }

            if (!InputRules.IsMoney(promotion.MinimumPurchase))
            {
                return $"Minimum purchase must be between 0 and {InputRules.MaxMoney:N2} with at most 2 decimal places.";
            }

            if (promotion.RequiredLoyaltyPoints < 0 || promotion.RequiredLoyaltyPoints > InputRules.MaxPoints)
            {
                return $"Points required must be between 0 and {InputRules.MaxPoints:N0}.";
            }

            if (promotion.StartDate.Year < 2000 || promotion.EndDate.Year > 2100)
            {
                return "Enter valid start and end dates.";
            }

            if (promotion.StartDate.Date > promotion.EndDate.Date)
            {
                return "Start date cannot be later than end date.";
            }

            if (string.IsNullOrWhiteSpace(promotion.EligibilityCategory))
            {
                promotion.EligibilityCategory = null;
            }
            else
            {
                promotion.EligibilityCategory = InputRules.OneOf(promotion.EligibilityCategory, CustomerDiscountEligibility.Categories);

                if (promotion.EligibilityCategory == null)
                {
                    return "Eligibility category must be one of: " + string.Join(", ", CustomerDiscountEligibility.Categories) + ".";
                }
            }

            return null;
        }

        private static object ToResponse(Promotion p, PromotionAccess access, Dictionary<int, string> branchNames)
        {
            return new
            {
                p.RowGuid,
                p.UpdatedAt,
                p.PromotionId,
                p.PromotionName,
                p.Description,
                p.DiscountType,
                p.DiscountValue,
                p.MinimumPurchase,
                p.RequiredLoyaltyPoints,
                p.StartDate,
                p.EndDate,
                p.Status,
                p.EligibilityCategory,
                p.BranchId,
                BranchName = p.BranchId != null && branchNames.TryGetValue(p.BranchId.Value, out var name) ? name : null,
                CanManage = access.CanManage(p)
            };
        }

        private static ObjectResult NotYours()
        {
            return BranchStock.OtherBranch("Company-wide promotions and other branches' promotions can only be changed by an administrator.");
        }
    }
}
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
        public async Task<IActionResult> GetPromotions(int companyId, bool includeInactive = false)
        {
            await using var tenantDb = await _tenantFactory.CreateAsync(companyId);

            var query = tenantDb.Promotions.AsNoTracking();

            if (!includeInactive)
            {
                query = query.Where(x => x.Status == "Active");
            }

            var promotions = await query
                .OrderBy(x => x.PromotionId)
                .ToListAsync();

            return Ok(promotions);
        }

        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetPromotionById(int companyId, int id)
        {
            await using var tenantDb = await _tenantFactory.CreateAsync(companyId);

            var promotion = await tenantDb.Promotions
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.PromotionId == id);

            if (promotion == null)
            {
                return NotFound($"Promotion with id {id} not found.");
            }

            return Ok(promotion);
        }

        [HttpPost]
        public async Task<IActionResult> CreatePromotion(int companyId, Promotion promotion)
        {
            await using var tenantDb = await _tenantFactory.CreateAsync(companyId);

            tenantDb.Promotions.Add(promotion);
            await tenantDb.SaveChangesAsync();

            return Created(
                $"api/tenant/{companyId}/promotions/{promotion.PromotionId}",
                promotion);
        }

        [HttpPut("{id:int}")]
        public async Task<IActionResult> UpdatePromotion(int companyId, int id, Promotion updated)
        {
            await using var tenantDb = await _tenantFactory.CreateAsync(companyId);

            var promotion = await tenantDb.Promotions.FirstOrDefaultAsync(x => x.PromotionId == id);

            if (promotion == null)
            {
                return NotFound($"Promotion with id {id} not found.");
            }

            promotion.PromotionName = updated.PromotionName;
            promotion.Description = updated.Description;
            promotion.DiscountType = updated.DiscountType;
            promotion.DiscountValue = updated.DiscountValue;
            promotion.StartDate = updated.StartDate;
            promotion.EndDate = updated.EndDate;
            promotion.Status = updated.Status;

            await tenantDb.SaveChangesAsync();

            return Ok(promotion);
        }

        [HttpDelete("{id:int}")]
        public async Task<IActionResult> DeletePromotion(int companyId, int id)
        {
            await using var tenantDb = await _tenantFactory.CreateAsync(companyId);

            var promotion = await tenantDb.Promotions.FirstOrDefaultAsync(x => x.PromotionId == id);

            if (promotion == null)
            {
                return NotFound($"Promotion with id {id} not found.");
            }

            promotion.Status = "Inactive";
            await tenantDb.SaveChangesAsync();

            return NoContent();
        }

        [HttpPut("{id:int}/reactivate")]
        public async Task<IActionResult> ReactivatePromotion(int companyId, int id)
        {
            await using var tenantDb = await _tenantFactory.CreateAsync(companyId);

            var promotion = await tenantDb.Promotions.FirstOrDefaultAsync(x => x.PromotionId == id);

            if (promotion == null)
            {
                return NotFound($"Promotion with id {id} not found.");
            }

            promotion.Status = "Active";
            await tenantDb.SaveChangesAsync();

            return Ok(promotion);
        }
    }
}
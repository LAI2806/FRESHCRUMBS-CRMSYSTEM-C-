using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using freshcrumbs.CRM.domain.entities;
using freshcrumbs.CRM.infrastructure.services;

namespace freshcrumbs.CRM.api.Controllers
{
    [ApiController]
    [Route("api/tenant/{companyId:int}/feedback")]
    public class TenantFeedbackController : ControllerBase
    {
        private readonly ITenantDbContextFactory _tenantFactory;

        public TenantFeedbackController(ITenantDbContextFactory tenantFactory)
        {
            _tenantFactory = tenantFactory;
        }

        [HttpGet]
        public async Task<IActionResult> GetFeedback(int companyId, bool includeDeleted = false)
        {
            await using var tenantDb = await _tenantFactory.CreateAsync(companyId);

            var query = tenantDb.Feedbacks.AsNoTracking();

            if (!includeDeleted)
            {
                query = query.Where(x => !x.IsDeleted);
            }

            var feedback = await query
                .OrderBy(x => x.FeedbackId)
                .ToListAsync();

            return Ok(feedback);
        }
        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetFeedbackById(int companyId, int id)
        {
            await using var tenantDb = await _tenantFactory.CreateAsync(companyId);

            var feedback = await tenantDb.Feedbacks
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.FeedbackId == id);

            if (feedback == null)
            {
                return NotFound($"Feedback with id {id} not found.");
            }

            return Ok(feedback);
        }

        [HttpPost]
        public async Task<IActionResult> CreateFeedback(int companyId, Feedback feedback)
        {
            await using var tenantDb = await _tenantFactory.CreateAsync(companyId);

            var customerExists = await tenantDb.Customers
                .AnyAsync(x => x.CustomerId == feedback.CustomerId);

            if (!customerExists)
            {
                return BadRequest($"CustomerId {feedback.CustomerId} does not exist for this tenant.");
            }

            tenantDb.Feedbacks.Add(feedback);
            await tenantDb.SaveChangesAsync();

            return Created(
                $"api/tenant/{companyId}/feedback/{feedback.FeedbackId}",
                feedback);
        }

        [HttpPut("{id:int}")]
        public async Task<IActionResult> UpdateFeedback(int companyId, int id, Feedback updated)
        {
            await using var tenantDb = await _tenantFactory.CreateAsync(companyId);

            var feedback = await tenantDb.Feedbacks.FirstOrDefaultAsync(x => x.FeedbackId == id);

            if (feedback == null)
            {
                return NotFound($"Feedback with id {id} not found.");
            }

            feedback.Type = updated.Type;
            feedback.Category = updated.Category;
            feedback.Comment = updated.Comment;
            feedback.DateSubmitted = updated.DateSubmitted;

            await tenantDb.SaveChangesAsync();

            return Ok(feedback);
        }

        [HttpPut("{id:int}/status")]
        public async Task<IActionResult> UpdateFeedbackStatus(int companyId, int id, [FromBody] string status)
        {
            await using var tenantDb = await _tenantFactory.CreateAsync(companyId);

            var feedback = await tenantDb.Feedbacks.FirstOrDefaultAsync(x => x.FeedbackId == id);

            if (feedback == null)
            {
                return NotFound($"Feedback with id {id} not found.");
            }

            feedback.Status = status;
            await tenantDb.SaveChangesAsync();

            return Ok(feedback);
        }

        [HttpDelete("{id:int}")]
        public async Task<IActionResult> DeleteFeedback(int companyId, int id)
        {
            await using var tenantDb = await _tenantFactory.CreateAsync(companyId);

            var feedback = await tenantDb.Feedbacks.FirstOrDefaultAsync(x => x.FeedbackId == id);

            if (feedback == null)
            {
                return NotFound($"Feedback with id {id} not found.");
            }

            feedback.IsDeleted = true;
            await tenantDb.SaveChangesAsync();

            return NoContent();
        }
    }
}
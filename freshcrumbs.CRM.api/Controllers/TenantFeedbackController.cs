using freshcrumbs.CRM.api.Authorization;
using freshcrumbs.CRM.api.Services;
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

            // PREMIUM MANAGER / STAFF: records of their assigned branch only (never from the request).
            var scope = await BranchStock.GetScopeAsync(HttpContext, tenantDb);

            if (scope.Restricted)
            {
                query = query.Where(x => x.BranchId != null && x.BranchId == scope.BranchId);
            }

            var feedback = await query
                .OrderBy(x => x.FeedbackId)
                .ToListAsync();

            if (BranchStock.IsBranchingCompany(HttpContext))
            {
                var names = await BranchStock.BranchNamesAsync(tenantDb);

                foreach (var row in feedback)
                {
                    row.BranchName = row.BranchId != null && names.TryGetValue(row.BranchId.Value, out var name) ? name : null;
                }
            }

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

            if (!(await BranchStock.GetScopeAsync(HttpContext, tenantDb)).Allows(feedback.BranchId))
            {
                return BranchStock.OtherBranch("This feedback belongs to another branch.");
            }

            return Ok(feedback);
        }

        [HttpPost]
        public async Task<IActionResult> CreateFeedback(int companyId, Feedback feedback)
        {
            string? error = ValidateFeedback(feedback, null);
            if (error != null)
            {
                return BadRequest(InputRules.Message(error));
            }

            await using var tenantDb = await _tenantFactory.CreateAsync(companyId);

            var customerActive = await tenantDb.Customers
                .AnyAsync(x => x.CustomerId == feedback.CustomerId && x.Status == "Active");

            if (!customerActive)
            {
                return BadRequest(InputRules.Message("The selected customer does not exist or is inactive."));
            }

            // Branch where it was recorded (PREMIUM): from the account's assignment, never from the client.
            var recordingScope = await BranchStock.GetScopeAsync(HttpContext, tenantDb);

            if (recordingScope.Restricted && recordingScope.BranchId == null)
            {
                return BranchStock.NoBranchAssigned();
            }

            // Identity, status and links are decided here; nested objects in the request are never saved.
            feedback.FeedbackId = 0;
            feedback.RowGuid = Guid.NewGuid();
            feedback.Status = "Pending";
            feedback.IsDeleted = false;
            feedback.Customer = null;
            feedback.BranchId = await BranchStock.GetRecordingBranchIdAsync(HttpContext, tenantDb);
            feedback.BranchName = null;

            tenantDb.Feedbacks.Add(feedback);
            await tenantDb.SaveChangesAsync();

            return Created(
                $"api/tenant/{companyId}/feedback/{feedback.FeedbackId}",
                feedback);
        }

        [HttpPut("{id:int}")]
        [RequireTenantPermission(TenantPermissions.ManageFeedback)]
        public async Task<IActionResult> UpdateFeedback(int companyId, int id, Feedback updated)
        {
            await using var tenantDb = await _tenantFactory.CreateAsync(companyId);

            var feedback = await tenantDb.Feedbacks.FirstOrDefaultAsync(x => x.FeedbackId == id);

            if (feedback == null)
            {
                return NotFound($"Feedback with id {id} not found.");
            }

            if (!(await BranchStock.GetScopeAsync(HttpContext, tenantDb)).Allows(feedback.BranchId))
            {
                return BranchStock.OtherBranch("This feedback belongs to another branch.");
            }

            if (feedback.IsDeleted)
            {
                return Conflict(InputRules.Message("This feedback has been deleted and cannot be changed."));
            }

            string? error = ValidateFeedback(updated, feedback);
            if (error != null)
            {
                return BadRequest(InputRules.Message(error));
            }

            string? status = InputRules.OneOf(updated.Status, Statuses);
            if (status == null)
            {
                return BadRequest(InputRules.Message("Status must be Pending, Reviewed or Resolved."));
            }

            feedback.Type = updated.Type;
            feedback.Category = updated.Category;
            feedback.Comment = updated.Comment;
            feedback.DateSubmitted = updated.DateSubmitted;
            feedback.Status = status;

            await tenantDb.SaveChangesAsync();

            return Ok(feedback);
        }

        [HttpPut("{id:int}/status")]
        [RequireTenantPermission(TenantPermissions.ManageFeedback)]
        public async Task<IActionResult> UpdateFeedbackStatus(int companyId, int id, [FromBody] string status)
        {
            await using var tenantDb = await _tenantFactory.CreateAsync(companyId);

            var feedback = await tenantDb.Feedbacks.FirstOrDefaultAsync(x => x.FeedbackId == id);

            if (feedback == null)
            {
                return NotFound($"Feedback with id {id} not found.");
            }

            if (!(await BranchStock.GetScopeAsync(HttpContext, tenantDb)).Allows(feedback.BranchId))
            {
                return BranchStock.OtherBranch("This feedback belongs to another branch.");
            }

            string? allowedStatus = InputRules.OneOf(status, Statuses);
            if (allowedStatus == null)
            {
                return BadRequest(InputRules.Message("Status must be Pending, Reviewed or Resolved."));
            }

            feedback.Status = allowedStatus;
            await tenantDb.SaveChangesAsync();

            return Ok(feedback);
        }

        private static readonly string[] Statuses = { "Pending", "Reviewed", "Resolved" };
        private static readonly string[] Types = { "Complaint", "Feedback", "Suggestion" };
        private static readonly string[] Categories =
        {
            "Customer Service", "Product Quality", "Product Availability", "Orders", "Pricing and Payments", "Packaging"
        };

        // Type and category from the desktop lists (an older value already on the record may be kept as it is);
        // the comment is free text up to the column limit; the date cannot be in the future.
        private static string? ValidateFeedback(Feedback feedback, Feedback? existing)
        {
            string? type = InputRules.OneOf(feedback.Type, Types)
                ?? (existing != null && feedback.Type == existing.Type ? existing.Type : null);

            if (type == null)
            {
                return "Type must be Complaint, Feedback or Suggestion.";
            }

            string? category = InputRules.OneOf(feedback.Category, Categories)
                ?? (existing != null && feedback.Category == existing.Category ? existing.Category : null);

            if (category == null)
            {
                return "Please select a valid category.";
            }

            string? error = InputRules.Text(feedback.Comment, "Comment", 1000, true, out var comment);
            if (error != null)
            {
                return error;
            }

            if (!InputRules.IsRecordDate(feedback.DateSubmitted))
            {
                return "The date must be a valid date and cannot be in the future.";
            }

            feedback.Type = type;
            feedback.Category = category;
            feedback.Comment = comment;
            return null;
        }

        [HttpDelete("{id:int}")]
        [RequireTenantPermission(TenantPermissions.ManageFeedback)]
        public async Task<IActionResult> DeleteFeedback(int companyId, int id)
        {
            await using var tenantDb = await _tenantFactory.CreateAsync(companyId);

            var feedback = await tenantDb.Feedbacks.FirstOrDefaultAsync(x => x.FeedbackId == id);

            if (feedback == null)
            {
                return NotFound($"Feedback with id {id} not found.");
            }

            if (!(await BranchStock.GetScopeAsync(HttpContext, tenantDb)).Allows(feedback.BranchId))
            {
                return BranchStock.OtherBranch("This feedback belongs to another branch.");
            }

            feedback.IsDeleted = true;
            await tenantDb.SaveChangesAsync();

            return NoContent();
        }
    }
}
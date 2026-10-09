using freshcrumbs.CRM.api.Authorization;
using freshcrumbs.CRM.api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using freshcrumbs.CRM.domain.entities;
using freshcrumbs.CRM.infrastructure.services;

namespace freshcrumbs.CRM.api.Controllers
{
    [ApiController]
    [Route("api/tenant/{companyId:int}/inquiries")]
    public class TenantInquiriesController : ControllerBase
    {
        private static readonly string[] AllowedStatuses = { "Pending", "In Progress", "Completed" };

        private readonly ITenantDbContextFactory _tenantFactory;

        public TenantInquiriesController(ITenantDbContextFactory tenantFactory)
        {
            _tenantFactory = tenantFactory;
        }

        [HttpGet]
        public async Task<IActionResult> GetInquiries(int companyId, bool includeDeleted = false)
        {
            await using var tenantDb = await _tenantFactory.CreateAsync(companyId);

            var query = tenantDb.Inquiries.AsNoTracking();

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

            var inquiries = await query
                .OrderBy(x => x.InquiryId)
                .ToListAsync();

            if (BranchStock.IsBranchingCompany(HttpContext))
            {
                var names = await BranchStock.BranchNamesAsync(tenantDb);

                foreach (var row in inquiries)
                {
                    row.BranchName = row.BranchId != null && names.TryGetValue(row.BranchId.Value, out var name) ? name : null;
                }
            }

            return Ok(inquiries);
        }

        [HttpPost]
        public async Task<IActionResult> CreateInquiry(int companyId, Inquiry inquiry)
        {
            string? error = ValidateInquiry(inquiry, null);
            if (error != null)
            {
                return BadRequest(InputRules.Message(error));
            }

            await using var tenantDb = await _tenantFactory.CreateAsync(companyId);

            var customerActive = await tenantDb.Customers
                .AnyAsync(x => x.CustomerId == inquiry.CustomerId && x.Status == "Active");

            if (!customerActive)
            {
                return BadRequest(InputRules.Message("The selected customer does not exist or is inactive."));
            }

            inquiry.Status = "Pending";
            inquiry.DateSubmitted = DateTime.UtcNow;
            inquiry.Subject = string.Empty;
            inquiry.Response = string.Empty;
            inquiry.RespondedBy = string.Empty;
            inquiry.RespondedAt = null;

            // Branch where it was recorded (PREMIUM): from the account's assignment, never from the client.
            var recordingScope = await BranchStock.GetScopeAsync(HttpContext, tenantDb);

            if (recordingScope.Restricted && recordingScope.BranchId == null)
            {
                return BranchStock.NoBranchAssigned();
            }

            inquiry.InquiryId = 0;
            inquiry.RowGuid = Guid.NewGuid();
            inquiry.IsDeleted = false;
            inquiry.Customer = null;
            inquiry.BranchId = await BranchStock.GetRecordingBranchIdAsync(HttpContext, tenantDb);
            inquiry.BranchName = null;

            tenantDb.Inquiries.Add(inquiry);
            await tenantDb.SaveChangesAsync();

            return Created(
                $"api/tenant/{companyId}/inquiries/{inquiry.InquiryId}",
                inquiry);
        }

        [HttpPut("{id:int}")]
        [RequireTenantPermission(TenantPermissions.ManageInquiries)]
        public async Task<IActionResult> UpdateInquiry(int companyId, int id, Inquiry updated)
        {
            await using var tenantDb = await _tenantFactory.CreateAsync(companyId);

            var inquiry = await tenantDb.Inquiries.FirstOrDefaultAsync(x => x.InquiryId == id);

            if (inquiry == null)
            {
                return NotFound($"Inquiry with id {id} not found.");
            }

            if (!(await BranchStock.GetScopeAsync(HttpContext, tenantDb)).Allows(inquiry.BranchId))
            {
                return BranchStock.OtherBranch("This inquiry belongs to another branch.");
            }

            if (inquiry.IsDeleted)
            {
                return Conflict(InputRules.Message("This inquiry has been deleted and cannot be changed."));
            }

            string? error = ValidateInquiry(updated, inquiry);
            if (error != null)
            {
                return BadRequest(InputRules.Message(error));
            }

            string? status = InputRules.OneOf(updated.Status, AllowedStatuses);
            if (status == null)
            {
                return BadRequest(InputRules.Message($"Status must be one of: {string.Join(", ", AllowedStatuses)}."));
            }

            updated.Status = status;

            if (string.Equals(updated.Status, "Completed", StringComparison.OrdinalIgnoreCase) &&
                string.IsNullOrWhiteSpace(updated.Response))
            {
                return BadRequest("A response is required before an inquiry can be marked Completed.");
            }

            inquiry.Type = updated.Type;
            inquiry.Source = updated.Source;
            inquiry.Message = updated.Message;
            inquiry.Status = updated.Status;

            if (!string.IsNullOrWhiteSpace(updated.Response) &&
                !string.Equals(updated.Response, inquiry.Response, StringComparison.Ordinal))
            {
                inquiry.Response = updated.Response;
                inquiry.RespondedBy = "Staff";
                inquiry.RespondedAt = DateTime.UtcNow;
            }

            await tenantDb.SaveChangesAsync();

            return Ok(inquiry);
        }

        private static readonly string[] Types = { "Product", "Order", "Payment", "Promotion", "Other" };
        private static readonly string[] Sources = { "Phone Call", "Email", "Facebook", "Walk-in", "Other" };

        // Type and source from the desktop lists (an older value already on the record may be kept as it is);
        // the concern and response are free text up to the column limits.
        private static string? ValidateInquiry(Inquiry inquiry, Inquiry? existing)
        {
            string? type = InputRules.OneOf(inquiry.Type, Types)
                ?? (existing != null && inquiry.Type == existing.Type ? existing.Type : null);

            if (type == null)
            {
                return "Please select a valid type.";
            }

            string? source = InputRules.OneOf(inquiry.Source, Sources)
                ?? (existing != null && inquiry.Source == existing.Source ? existing.Source : null);

            if (source == null)
            {
                return "Please select a valid source.";
            }

            string? messageError = InputRules.Text(inquiry.Message, "Concern", 1000, true, out var message);
            string? responseError = InputRules.Text(inquiry.Response, "Response", 1000, false, out var response);
            string? error = messageError ?? responseError;

            if (error != null)
            {
                return error;
            }

            inquiry.Type = type;
            inquiry.Source = source;
            inquiry.Message = message;
            inquiry.Response = response;
            return null;
        }

        [HttpDelete("{id:int}")]
        [RequireTenantPermission(TenantPermissions.ManageInquiries)]
        public async Task<IActionResult> DeleteInquiry(int companyId, int id)
        {
            await using var tenantDb = await _tenantFactory.CreateAsync(companyId);

            var inquiry = await tenantDb.Inquiries.FirstOrDefaultAsync(x => x.InquiryId == id);

            if (inquiry == null)
            {
                return NotFound($"Inquiry with id {id} not found.");
            }

            if (!(await BranchStock.GetScopeAsync(HttpContext, tenantDb)).Allows(inquiry.BranchId))
            {
                return BranchStock.OtherBranch("This inquiry belongs to another branch.");
            }

            inquiry.IsDeleted = true;
            await tenantDb.SaveChangesAsync();

            return NoContent();
        }
    }
}
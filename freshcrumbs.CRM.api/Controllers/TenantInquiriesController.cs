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

            var inquiries = await query
                .OrderBy(x => x.InquiryId)
                .ToListAsync();

            return Ok(inquiries);
        }

        [HttpPost]
        public async Task<IActionResult> CreateInquiry(int companyId, Inquiry inquiry)
        {
            await using var tenantDb = await _tenantFactory.CreateAsync(companyId);

            var customerExists = await tenantDb.Customers
                .AnyAsync(x => x.CustomerId == inquiry.CustomerId);

            if (!customerExists)
            {
                return BadRequest($"CustomerId {inquiry.CustomerId} does not exist for this tenant.");
            }

            if (string.IsNullOrWhiteSpace(inquiry.Type))
            {
                return BadRequest("Type is required.");
            }

            if (string.IsNullOrWhiteSpace(inquiry.Source))
            {
                return BadRequest("Source is required.");
            }

            if (string.IsNullOrWhiteSpace(inquiry.Message))
            {
                return BadRequest("Concern is required.");
            }

            inquiry.Status = "Pending";
            inquiry.DateSubmitted = DateTime.UtcNow;
            inquiry.Subject = string.Empty;
            inquiry.Response = string.Empty;
            inquiry.RespondedBy = string.Empty;
            inquiry.RespondedAt = null;

            tenantDb.Inquiries.Add(inquiry);
            await tenantDb.SaveChangesAsync();

            return Created(
                $"api/tenant/{companyId}/inquiries/{inquiry.InquiryId}",
                inquiry);
        }

        [HttpPut("{id:int}")]
        public async Task<IActionResult> UpdateInquiry(int companyId, int id, Inquiry updated)
        {
            await using var tenantDb = await _tenantFactory.CreateAsync(companyId);

            var inquiry = await tenantDb.Inquiries.FirstOrDefaultAsync(x => x.InquiryId == id);

            if (inquiry == null)
            {
                return NotFound($"Inquiry with id {id} not found.");
            }

            if (string.IsNullOrWhiteSpace(updated.Type))
            {
                return BadRequest("Type is required.");
            }

            if (string.IsNullOrWhiteSpace(updated.Source))
            {
                return BadRequest("Source is required.");
            }

            if (string.IsNullOrWhiteSpace(updated.Message))
            {
                return BadRequest("Concern is required.");
            }

            if (!AllowedStatuses.Contains(updated.Status, StringComparer.OrdinalIgnoreCase))
            {
                return BadRequest($"Status must be one of: {string.Join(", ", AllowedStatuses)}.");
            }

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

        [HttpDelete("{id:int}")]
        public async Task<IActionResult> DeleteInquiry(int companyId, int id)
        {
            await using var tenantDb = await _tenantFactory.CreateAsync(companyId);

            var inquiry = await tenantDb.Inquiries.FirstOrDefaultAsync(x => x.InquiryId == id);

            if (inquiry == null)
            {
                return NotFound($"Inquiry with id {id} not found.");
            }

            inquiry.IsDeleted = true;
            await tenantDb.SaveChangesAsync();

            return NoContent();
        }
    }
}
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
        private readonly ITenantDbContextFactory _tenantFactory;

        public TenantInquiriesController(ITenantDbContextFactory tenantFactory)
        {
            _tenantFactory = tenantFactory;
        }

        [HttpGet]
        public async Task<IActionResult> GetInquiries(int companyId)
        {
            await using var tenantDb = await _tenantFactory.CreateAsync(companyId);

            var inquiries = await tenantDb.Inquiries
                .AsNoTracking()
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

            inquiry.Status = "Pending";
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

            inquiry.Subject = updated.Subject;
            inquiry.Message = updated.Message;

            await tenantDb.SaveChangesAsync();

            return Ok(inquiry);
        }

        [HttpPut("{id:int}/status")]
        public async Task<IActionResult> UpdateInquiryStatus(int companyId, int id, [FromBody] string status)
        {
            await using var tenantDb = await _tenantFactory.CreateAsync(companyId);

            var inquiry = await tenantDb.Inquiries.FirstOrDefaultAsync(x => x.InquiryId == id);

            if (inquiry == null)
            {
                return NotFound($"Inquiry with id {id} not found.");
            }

            inquiry.Status = status;
            await tenantDb.SaveChangesAsync();

            return Ok(inquiry);
        }

        [HttpPut("{id:int}/respond")]
        public async Task<IActionResult> RespondToInquiry(int companyId, int id, [FromBody] RespondRequest request)
        {
            await using var tenantDb = await _tenantFactory.CreateAsync(companyId);

            var inquiry = await tenantDb.Inquiries.FirstOrDefaultAsync(x => x.InquiryId == id);

            if (inquiry == null)
            {
                return NotFound($"Inquiry with id {id} not found.");
            }

            inquiry.Response = request.Response;
            inquiry.RespondedBy = request.RespondedBy;
            inquiry.RespondedAt = DateTime.UtcNow;
            inquiry.Status = "Answered";

            await tenantDb.SaveChangesAsync();

            return Ok(inquiry);
        }

        public class RespondRequest
        {
            public string Response { get; set; } = string.Empty;
            public string RespondedBy { get; set; } = string.Empty;
        }
    }
}
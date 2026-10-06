using System.Text.RegularExpressions;
using freshcrumbs.CRM.domain.entities;
using freshcrumbs.CRM.infrastructure.data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace freshcrumbs.CRM.api.Controllers
{
    [ApiController]
    [Authorize(Roles = PlatformRoles.SuperAdmin)]
    [Route("api/platform/tenants")]
    public class TenantsController : ControllerBase
    {
        private static readonly Regex EmailPattern = new(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.Compiled);
        private static readonly Regex PhonePattern = new(@"^[0-9+\-\s()]{7,20}$", RegexOptions.Compiled);

        private readonly MasterCrmDbContext _db;

        public TenantsController(MasterCrmDbContext db)
        {
            _db = db;
        }

        public class UpdateTenantRequest
        {
            public string CompanyName { get; set; } = string.Empty;
            public string BusinessAddress { get; set; } = string.Empty;
            public string ContactNo { get; set; } = string.Empty;
            public string Email { get; set; } = string.Empty;
        }

        [HttpPut("{companyId:int}")]
        public async Task<IActionResult> Update(int companyId, UpdateTenantRequest request)
        {
            var company = await _db.Companies.FirstOrDefaultAsync(c => c.CompanyId == companyId);

            if (company == null)
            {
                return NotFound(new { message = "Tenant not found." });
            }

            var name = (request.CompanyName ?? string.Empty).Trim();
            var address = (request.BusinessAddress ?? string.Empty).Trim();
            var contact = (request.ContactNo ?? string.Empty).Trim();
            var email = (request.Email ?? string.Empty).Trim();

            if (name.Length == 0 || name.Length > 200)
            {
                return BadRequest(new { message = "Business name is required (up to 200 characters)." });
            }

            if (address.Length == 0 || address.Length > 300)
            {
                return BadRequest(new { message = "Business address is required (up to 300 characters)." });
            }

            if (!PhonePattern.IsMatch(contact))
            {
                return BadRequest(new { message = "Contact number must be 7 to 20 characters (digits, +, -, spaces, parentheses)." });
            }

            if (email.Length > 150 || !EmailPattern.IsMatch(email))
            {
                return BadRequest(new { message = "A valid email address (up to 150 characters) is required." });
            }

            company.CompanyName = name;
            company.BusinessAddress = address;
            company.ContactNo = contact;
            company.Email = email;

            await _db.SaveChangesAsync();

            return Ok(new
            {
                company.CompanyId,
                company.CompanyCode,
                company.CompanyName,
                company.BusinessAddress,
                company.ContactNo,
                company.Email
            });
        }
    }
}
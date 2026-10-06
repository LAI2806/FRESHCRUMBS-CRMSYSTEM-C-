using System.Security.Claims;
using freshcrumbs.CRM.api.Services;
using freshcrumbs.CRM.domain.entities;
using freshcrumbs.CRM.infrastructure.data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace freshcrumbs.CRM.api.Controllers
{
    [ApiController]
    [Route("api/tenant/{companyId:int}/terms")]
    public class TenantTermsController : ControllerBase
    {
        private readonly MasterCrmDbContext _db;
        private readonly ITermsService _terms;

        public TenantTermsController(MasterCrmDbContext db, ITermsService terms)
        {
            _db = db;
            _terms = terms;
        }

        public class AcceptRequest
        {
            public int TermsVersionId { get; set; }
        }

        // Authentication, company match and subscription are enforced by TenantAccessFilter.
        [HttpGet]
        public async Task<IActionResult> GetCurrent(int companyId)
        {
            return Ok(await BuildAsync(companyId));
        }

        [HttpPost("accept")]
        public async Task<IActionResult> Accept(int companyId, AcceptRequest request)
        {
            var current = await _terms.GetCurrentAsync();

            if (current == null)
            {
                return Conflict(new { message = "There are no published terms to accept." });
            }

            if (current.TermsVersionId != request.TermsVersionId)
            {
                return Conflict(new { message = "A newer version of the Terms & Conditions was published. Please review the latest version." });
            }

            var already = await _db.TermsAcceptances
                .AnyAsync(a => a.CompanyId == companyId && a.TermsVersionId == current.TermsVersionId);

            if (!already)
            {
                _db.TermsAcceptances.Add(new TermsAcceptance
                {
                    TermsVersionId = current.TermsVersionId,
                    CompanyId = companyId,
                    UserId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty,
                    UserName = User.Identity?.Name ?? string.Empty,
                    AcceptedAt = DateTime.UtcNow
                });

                try
                {
                    await _db.SaveChangesAsync();
                }
                catch (DbUpdateException)
                {
                    // Another user of the same company accepted at the same moment: that is fine.
                }
            }

            return Ok(await BuildAsync(companyId));
        }

        private async Task<object> BuildAsync(int companyId)
        {
            var current = await _terms.GetCurrentAsync();

            if (current == null)
            {
                return new { HasTerms = false, PendingAcceptance = false };
            }

            var acceptance = await _db.TermsAcceptances.AsNoTracking()
                .FirstOrDefaultAsync(a => a.CompanyId == companyId && a.TermsVersionId == current.TermsVersionId);

            return new
            {
                HasTerms = true,
                current.TermsVersionId,
                current.VersionNumber,
                current.Title,
                current.Content,
                PublishedAt = current.PublishedAt == null ? (DateTime?)null : DateTime.SpecifyKind(current.PublishedAt.Value, DateTimeKind.Utc),
                current.RequiresAcceptance,
                Accepted = acceptance != null,
                AcceptedAt = acceptance == null ? (DateTime?)null : DateTime.SpecifyKind(acceptance.AcceptedAt, DateTimeKind.Utc),
                AcceptedBy = acceptance?.UserName,
                PendingAcceptance = current.RequiresAcceptance && acceptance == null
            };
        }
    }
}
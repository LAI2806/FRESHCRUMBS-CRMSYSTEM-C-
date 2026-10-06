using freshcrumbs.CRM.domain.entities;
using freshcrumbs.CRM.infrastructure.data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace freshcrumbs.CRM.api.Controllers
{
    [ApiController]
    [Authorize(Roles = PlatformRoles.SuperAdmin)]
    [Route("api/platform/terms")]
    public class TermsController : ControllerBase
    {
        private const int MaxTitleLength = 200;
        private const int MaxContentLength = 100000;

        private readonly MasterCrmDbContext _db;

        public TermsController(MasterCrmDbContext db)
        {
            _db = db;
        }

        public class TermsRequest
        {
            public string Title { get; set; } = string.Empty;
            public string Content { get; set; } = string.Empty;
        }

        public class PublishRequest
        {
            public bool RequiresAcceptance { get; set; } = true;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var versions = await _db.TermsVersions.AsNoTracking()
                .OrderByDescending(t => t.VersionNumber)
                .Select(t => new
                {
                    t.TermsVersionId,
                    t.VersionNumber,
                    t.Title,
                    t.Status,
                    t.CreatedAt,
                    t.CreatedBy,
                    t.UpdatedAt,
                    t.PublishedAt,
                    t.PublishedBy,
                    t.ArchivedAt,
                    t.RequiresAcceptance
                })
                .ToListAsync();

            var accepted = await _db.TermsAcceptances.AsNoTracking()
                .GroupBy(a => a.TermsVersionId)
                .Select(g => new { Id = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.Id, x => x.Count);

            return Ok(versions.Select(t => new
            {
                t.TermsVersionId,
                t.VersionNumber,
                t.Title,
                Status = t.Status.ToString(),
                CreatedAt = Utc(t.CreatedAt),
                t.CreatedBy,
                UpdatedAt = Utc(t.UpdatedAt),
                PublishedAt = Utc(t.PublishedAt),
                t.PublishedBy,
                ArchivedAt = Utc(t.ArchivedAt),
                t.RequiresAcceptance,
                AcceptedCompanies = accepted.TryGetValue(t.TermsVersionId, out var count) ? count : 0
            }));
        }

        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetById(int id)
        {
            var terms = await _db.TermsVersions.AsNoTracking().FirstOrDefaultAsync(t => t.TermsVersionId == id);

            if (terms == null)
            {
                return NotFound(new { message = "Terms version not found." });
            }

            var accepted = await _db.TermsAcceptances.CountAsync(a => a.TermsVersionId == id);

            return Ok(ToDetail(terms, accepted));
        }

        [HttpPost]
        public async Task<IActionResult> CreateDraft(TermsRequest request)
        {
            var error = Validate(request);

            if (error != null)
            {
                return BadRequest(new { message = error });
            }

            var terms = new TermsVersion
            {
                Title = request.Title.Trim(),
                Content = request.Content.Trim(),
                Status = TermsStatus.Draft,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = User.Identity?.Name ?? "unknown",
                RequiresAcceptance = false
            };

            _db.TermsVersions.Add(terms);

            for (var attempt = 0; ; attempt++)
            {
                terms.VersionNumber = (await _db.TermsVersions.MaxAsync(t => (int?)t.VersionNumber) ?? 0) + 1;

                try
                {
                    await _db.SaveChangesAsync();
                    break;
                }
                catch (DbUpdateException ex) when (attempt < 4 && ex.InnerException?.Message.Contains("IX_TermsVersions_VersionNumber") == true)
                {
                }
            }

            return Created($"api/platform/terms/{terms.TermsVersionId}", ToDetail(terms, 0));
        }

        [HttpPut("{id:int}")]
        public async Task<IActionResult> UpdateDraft(int id, TermsRequest request)
        {
            var terms = await _db.TermsVersions.FirstOrDefaultAsync(t => t.TermsVersionId == id);

            if (terms == null)
            {
                return NotFound(new { message = "Terms version not found." });
            }

            if (terms.Status != TermsStatus.Draft)
            {
                return Conflict(new { message = "Published terms cannot be changed. Create a new version instead." });
            }

            var error = Validate(request);

            if (error != null)
            {
                return BadRequest(new { message = error });
            }

            terms.Title = request.Title.Trim();
            terms.Content = request.Content.Trim();
            terms.UpdatedAt = DateTime.UtcNow;

            await _db.SaveChangesAsync();

            return Ok(ToDetail(terms, 0));
        }

        [HttpDelete("{id:int}")]
        public async Task<IActionResult> DeleteDraft(int id)
        {
            var terms = await _db.TermsVersions.FirstOrDefaultAsync(t => t.TermsVersionId == id);

            if (terms == null)
            {
                return NotFound(new { message = "Terms version not found." });
            }

            if (terms.Status != TermsStatus.Draft)
            {
                return Conflict(new { message = "Only drafts can be deleted. Published versions are kept as history." });
            }

            _db.TermsVersions.Remove(terms);
            await _db.SaveChangesAsync();

            return NoContent();
        }

        [HttpPost("{id:int}/publish")]
        public async Task<IActionResult> Publish(int id, PublishRequest request)
        {
            var terms = await _db.TermsVersions.FirstOrDefaultAsync(t => t.TermsVersionId == id);

            if (terms == null)
            {
                return NotFound(new { message = "Terms version not found." });
            }

            if (terms.Status != TermsStatus.Draft)
            {
                return Conflict(new { message = "Only a draft can be published." });
            }

            var error = Validate(new TermsRequest { Title = terms.Title, Content = terms.Content });

            if (error != null)
            {
                return BadRequest(new { message = error });
            }

            var now = DateTime.UtcNow;

            // Only one version is published at a time: the previous one becomes history.
            var previous = await _db.TermsVersions.Where(t => t.Status == TermsStatus.Published).ToListAsync();

            foreach (var old in previous)
            {
                old.Status = TermsStatus.Archived;
                old.ArchivedAt = now;
            }

            terms.Status = TermsStatus.Published;
            terms.PublishedAt = now;
            terms.PublishedBy = User.Identity?.Name ?? "unknown";
            terms.RequiresAcceptance = request.RequiresAcceptance;

            await _db.SaveChangesAsync();

            return Ok(ToDetail(terms, 0));
        }

        [HttpGet("{id:int}/acceptances")]
        public async Task<IActionResult> GetAcceptances(int id)
        {
            if (!await _db.TermsVersions.AnyAsync(t => t.TermsVersionId == id))
            {
                return NotFound(new { message = "Terms version not found." });
            }

            var rows = await _db.TermsAcceptances.AsNoTracking()
                .Where(a => a.TermsVersionId == id)
                .Join(_db.Companies, a => a.CompanyId, c => c.CompanyId, (a, c) => new
                {
                    a.CompanyId,
                    c.CompanyCode,
                    c.CompanyName,
                    a.UserName,
                    a.AcceptedAt
                })
                .OrderByDescending(x => x.AcceptedAt)
                .ToListAsync();

            return Ok(rows.Select(x => new
            {
                x.CompanyId,
                x.CompanyCode,
                x.CompanyName,
                x.UserName,
                AcceptedAt = Utc(x.AcceptedAt)
            }));
        }

        private static string? Validate(TermsRequest request)
        {
            var title = (request.Title ?? string.Empty).Trim();
            var content = (request.Content ?? string.Empty).Trim();

            if (title.Length == 0)
            {
                return "Title is required.";
            }

            if (title.Length > MaxTitleLength)
            {
                return $"Title cannot exceed {MaxTitleLength} characters.";
            }

            if (content.Length == 0)
            {
                return "Content is required.";
            }

            if (content.Length > MaxContentLength)
            {
                return $"Content cannot exceed {MaxContentLength:N0} characters.";
            }

            return null;
        }

        private static DateTime? Utc(DateTime? value) =>
            value == null ? null : DateTime.SpecifyKind(value.Value, DateTimeKind.Utc);

        private static DateTime Utc(DateTime value) => DateTime.SpecifyKind(value, DateTimeKind.Utc);

        private static object ToDetail(TermsVersion t, int acceptedCompanies)
        {
            return new
            {
                t.TermsVersionId,
                t.VersionNumber,
                t.Title,
                t.Content,
                Status = t.Status.ToString(),
                CreatedAt = Utc(t.CreatedAt),
                t.CreatedBy,
                UpdatedAt = Utc(t.UpdatedAt),
                PublishedAt = Utc(t.PublishedAt),
                t.PublishedBy,
                ArchivedAt = Utc(t.ArchivedAt),
                t.RequiresAcceptance,
                AcceptedCompanies = acceptedCompanies
            };
        }
    }
}
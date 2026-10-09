using System.Net;
using System.Security.Claims;
using System.Text.Json;
using freshcrumbs.CRM.api.Services;
using freshcrumbs.CRM.api.Services.Sync;
using freshcrumbs.CRM.domain.entities;
using freshcrumbs.CRM.infrastructure.data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace freshcrumbs.CRM.api.Controllers
{
    // Terms are company-level: any signed-in user of the company (ADMIN, MANAGER or STAFF) may read and accept the
    // current version for the whole company; who accepted is recorded. The cloud master database is the only source:
    // the desktop (Local mode) forwards both endpoints to the cloud and never keeps Terms of its own.
    [ApiController]
    [Route("api/tenant/{companyId:int}/terms")]
    public class TenantTermsController : ControllerBase
    {
        private readonly MasterCrmDbContext _db;
        private readonly ITermsService _terms;
        private readonly SyncOptions _syncOptions;
        private readonly IServiceProvider _services;

        public TenantTermsController(MasterCrmDbContext db, ITermsService terms, SyncOptions syncOptions, IServiceProvider services)
        {
            _db = db;
            _terms = terms;
            _syncOptions = syncOptions;
            _services = services;
        }

        public class AcceptRequest
        {
            public int TermsVersionId { get; set; }
        }

        // Authentication, company match and subscription are enforced by TenantAccessFilter.
        [HttpGet]
        public async Task<IActionResult> GetCurrent(int companyId)
        {
            if (_syncOptions.IsLocal)
            {
                return await GetFromCloudAsync(companyId);
            }

            return Ok(await BuildAsync(companyId));
        }

        [HttpPost("accept")]
        public async Task<IActionResult> Accept(int companyId, AcceptRequest request)
        {
            if (_syncOptions.IsLocal)
            {
                return await AcceptInCloudAsync(companyId, request);
            }

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

        // ---------------------------------------------------------------- Local mode (desktop)

        private async Task<IActionResult> GetFromCloudAsync(int companyId)
        {
            var call = await SendToCloudAsync(HttpMethod.Get, $"api/tenant/{companyId}/terms", null);

            if (call.Data is JsonElement data)
            {
                return Ok(data);
            }

            if (!call.Offline)
            {
                return call.Error!;
            }

            // No cloud: the pending version from the last online sign-in still applies, so it can never be skipped.
            var cache = _services.GetRequiredService<LocalAccessCache>();
            var pending = cache.Read(d => d.Companies.TryGetValue(companyId, out var c) ? c.Snapshot.PendingTermsVersion : null);

            if (pending != null)
            {
                return CloudRequired(
                    $"Version {pending} of the Terms & Conditions must be accepted before FreshCrumbs can be used. " +
                    "Connect to the internet and sign in again to review and accept them.");
            }

            return Ok(new { HasTerms = false, PendingAcceptance = false });
        }

        private async Task<IActionResult> AcceptInCloudAsync(int companyId, AcceptRequest request)
        {
            var call = await SendToCloudAsync(
                HttpMethod.Post, $"api/tenant/{companyId}/terms/accept", new { termsVersionId = request.TermsVersionId });

            if (call.Data is not JsonElement data)
            {
                return call.Error!;
            }

            // The local access check reads PendingTermsVersion from the snapshot: take the cloud's new one now.
            var login = _services.GetRequiredService<LocalLoginService>();

            if (!await login.RefreshCompanySnapshotAsync(call.Token!, companyId, HttpContext.RequestAborted))
            {
                return CloudRequired(
                    "The Terms & Conditions were accepted, but this computer could not refresh the company details. " +
                    "Sign out and sign in again while connected to the internet.");
            }

            return Ok(data);
        }

        private sealed record CloudCall(JsonElement? Data, IActionResult? Error, bool Offline, string? Token);

        private async Task<CloudCall> SendToCloudAsync(HttpMethod method, string relativeUrl, object? body)
        {
            var sessions = _services.GetRequiredService<CloudSessionTokens>();
            var callerId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var token = sessions.Get(callerId);

            if (token == null)
            {
                return new CloudCall(null, CloudRequired(
                    "The Terms & Conditions need an online sign-in. Sign out, sign in again while connected to the internet, then try again."),
                    true, null);
            }

            var cloud = _services.GetRequiredService<CloudApiClient>();
            var result = await cloud.SendJsonAsync(method, relativeUrl, token, body, HttpContext.RequestAborted);

            if (result.Unreachable)
            {
                return new CloudCall(null, CloudRequired(
                    "The FreshCrumbs cloud service could not be reached. The Terms & Conditions need the internet."),
                    true, token);
            }

            if (!result.Ok)
            {
                if (result.StatusCode == HttpStatusCode.Unauthorized)
                {
                    sessions.Remove(callerId!);

                    return new CloudCall(null, CloudRequired(
                        "Your online session has expired. Sign out and sign in again while connected to the internet."),
                        true, null);
                }

                return new CloudCall(null, StatusCode(
                    (int)(result.StatusCode ?? HttpStatusCode.BadRequest),
                    new { message = result.Message ?? "The cloud service refused the request." }),
                    false, token);
            }

            return new CloudCall(result.Data, null, false, token);
        }

        // 503 (not 401) so the desktop never mistakes a missing cloud session for its own expired session.
        private ObjectResult CloudRequired(string message)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { code = "CloudRequired", message });
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
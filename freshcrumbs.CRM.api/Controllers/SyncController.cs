using System.Globalization;
using System.Security.Claims;
using System.Text.Json;
using freshcrumbs.CRM.api.Authorization;
using freshcrumbs.CRM.api.Services.Sync;
using freshcrumbs.CRM.domain.entities;
using freshcrumbs.CRM.infrastructure.services;
using freshcrumbs.CRM.infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace freshcrumbs.CRM.api.Controllers
{
    [ApiController]
    public class SyncController : ControllerBase
    {
        private const int MaxPullPage = 500;

        private readonly SyncOptions _options;
        private readonly ITenantDbContextFactory _factory;

        public SyncController(SyncOptions options, ITenantDbContextFactory factory)
        {
            _options = options;
            _factory = factory;
        }

        // Cheap, anonymous connectivity probe used by the desktop (cloud side).
        [HttpGet("api/sync/ping")]
        [AllowAnonymous]
        public IActionResult Ping()
        {
            return Ok(new { ok = true, serverTimeUtc = DateTime.UtcNow });
        }

        // ------------------------------------------------------------------ LOCAL (desktop) endpoints

        // Status for the WinForms status label. Tenant comes from the caller's own token.
        [HttpGet("api/sync/status")]
        [Authorize]
        public async Task<IActionResult> Status([FromServices] IServiceProvider services)
        {
            if (!_options.IsLocal)
            {
                return Ok(new SyncStatusDto { Mode = "Cloud", State = "Cloud", IsOnline = true });
            }

            var status = services.GetRequiredService<SyncStatusService>();
            var cache = services.GetRequiredService<LocalAccessCache>();

            var snapshot = status.Snapshot();
            var dto = new SyncStatusDto
            {
                Mode = "Local",
                State = snapshot.State,
                IsOnline = snapshot.Online,
                LastSyncUtc = snapshot.LastSyncUtc,
                LastError = snapshot.LastError
            };

            if (int.TryParse(User.FindFirstValue(TenantAccessRequirement.TenantIdClaim), out var companyId))
            {
                var grace = cache.GetGrace(companyId);
                dto.GraceDaysLeft = grace.Valid ? Math.Round(grace.DaysLeft, 1) : 0;

                try
                {
                    await using var db = await _factory.CreateAsync(companyId);
                    dto.Pending = await db.SyncOutbox.CountAsync(o => o.Status == SyncOutboxStatus.Pending, HttpContext.RequestAborted);
                    dto.Rejected = await db.SyncOutbox.CountAsync(o => o.Status == SyncOutboxStatus.Rejected, HttpContext.RequestAborted);
                }
                catch (Exception ex)
                {
                    dto.State = "Error";
                    dto.LastError = ex.GetBaseException().Message;
                }
            }

            return Ok(dto);
        }

        [HttpPost("api/sync/now")]
        [Authorize]
        public IActionResult SyncNow([FromServices] IEnumerable<IHostedService> hosted)
        {
            hosted.OfType<SyncWorker>().FirstOrDefault()?.SyncNow();
            return Accepted();
        }

        // ------------------------------------------------------------------ CLOUD endpoints
        // Reached through TenantAccessFilter: the token's company must match {companyId}, the account must
        // be active and the company must hold an active subscription.

        [HttpPost("api/tenant/{companyId:int}/sync/push")]
        public async Task<IActionResult> Push(
            int companyId,
            [FromBody] PushRequest request,
            [FromServices] CloudSyncApplier applier)
        {
            if (_options.IsLocal)
            {
                return NotFound();
            }

            if (request.Operations.Count == 0)
            {
                return Ok(new PushResponse { ServerTimeUtc = DateTime.UtcNow });
            }

            if (request.Operations.Count > 2000)
            {
                return BadRequest(new { message = "Too many operations in one request." });
            }

            var features = HttpContext.Items[TenantAccessFilter.FeaturesItemKey] as List<string> ?? new List<string>();

            return Ok(await applier.ApplyAsync(companyId, request, features, HttpContext.RequestAborted));
        }

        [HttpGet("api/tenant/{companyId:int}/sync/pull")]
        public async Task<IActionResult> Pull(
            int companyId,
            [FromQuery] string type,
            [FromQuery] string? since,
            [FromQuery] int skip = 0,
            [FromQuery] int take = 300)
        {
            if (_options.IsLocal)
            {
                return NotFound();
            }

            take = Math.Clamp(take, 1, MaxPullPage);
            skip = Math.Max(0, skip);

            var response = new PullResponse { ServerTimeUtc = DateTime.UtcNow };

            // Pull is data replication, not use of a feature: the desktop receives ALL of the company's
            // records whatever the current plan. Historical data (e.g. promotions referenced by old sales)
            // must survive a downgrade and already be there after an upgrade. What the plan allows the
            // user to DO stays enforced by TenantAccessFilter/TenantPermissions and by Push, which
            // rejects changes to features the plan does not include (CloudSyncApplier.ApplyAsync).
            // FeatureFor is used here only to recognise the synchronized entity types.
            if (CloudSyncApplier.FeatureFor(type) == null)
            {
                return Ok(response);
            }

            await using var db = await _factory.CreateAsync(companyId);
            var ct = HttpContext.RequestAborted;

            DateTime? sinceUtc = null;

            if (!string.IsNullOrWhiteSpace(since))
            {
                if (!DateTime.TryParse(since, CultureInfo.InvariantCulture,
                        DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var parsedSince))
                {
                    return BadRequest(new { message = "since must be an ISO-8601 UTC time." });
                }

                sinceUtc = DateTime.SpecifyKind(parsedSince, DateTimeKind.Utc);
            }

            // Changes made through push are tracked by their receipts (server clock), not by device clocks.
            IQueryable<Guid>? changed = sinceUtc == null
                ? null
                : db.SyncReceipts.AsNoTracking()
                    .Where(r => r.ProcessedAtUtc > sinceUtc && r.EntityType == type && r.Result == PushResult.Applied)
                    .Select(r => r.EntityRowGuid);

            List<ISyncEntity> page = type switch
            {
                nameof(Customer) => await Page(db.Customers.AsNoTracking(), x => x.CustomerId, changed, skip, take, ct),
                nameof(Product) => await Page(db.Products.AsNoTracking(), x => x.ProductId, changed, skip, take, ct),
                nameof(Promotion) => await Page(db.Promotions.AsNoTracking(), x => x.PromotionId, changed, skip, take, ct),
                nameof(CustomerDiscountEligibility) => await Page(db.CustomerDiscountEligibilities.AsNoTracking(), x => x.EligibilityId, changed, skip, take, ct),
                nameof(SalesTransaction) => await Page(db.SalesTransactions.AsNoTracking(), x => x.TransactionId, changed, skip, take, ct),
                nameof(TransactionItem) => await Page(db.TransactionItems.AsNoTracking(), x => x.TransactionItemId, changed, skip, take, ct),
                nameof(LoyaltyTransaction) => await Page(db.LoyaltyTransactions.AsNoTracking(), x => x.LoyaltyTransactionId, changed, skip, take, ct),
                nameof(Feedback) => await Page(db.Feedbacks.AsNoTracking(), x => x.FeedbackId, changed, skip, take, ct),
                nameof(Inquiry) => await Page(db.Inquiries.AsNoTracking(), x => x.InquiryId, changed, skip, take, ct),
                nameof(Branch) => await Page(db.Branches.AsNoTracking(), x => x.BranchId, changed, skip, take, ct),
                nameof(BranchInventory) => await Page(db.BranchInventories.AsNoTracking(), x => x.BranchInventoryId, changed, skip, take, ct),
                nameof(BranchAssignment) => await Page(db.BranchAssignments.AsNoTracking(), x => x.BranchAssignmentId, changed, skip, take, ct),
                _ => new List<ISyncEntity>()
            };

            response.HasMore = page.Count > take;

            var parentCache = new Dictionary<string, Guid?>();

            foreach (var entity in page.Take(take))
            {
                var payload = await SyncPayloadBuilder.BuildPullPayloadAsync(db, entity, parentCache, ct);

                response.Items.Add(new PullItemDto
                {
                    EntityType = type,
                    RowGuid = entity.RowGuid,
                    UpdatedAt = DateTime.SpecifyKind(entity.UpdatedAt, DateTimeKind.Utc),
                    Payload = JsonSerializer.SerializeToElement(payload)
                });
            }

            return Ok(response);
        }

        // Fetches take+1 rows so the caller knows whether another page exists.
        private static async Task<List<ISyncEntity>> Page<T>(
            IQueryable<T> source,
            System.Linq.Expressions.Expression<Func<T, int>> key,
            IQueryable<Guid>? changed,
            int skip,
            int take,
            CancellationToken ct) where T : class, ISyncEntity
        {
            if (changed != null)
            {
                source = source.Where(x => changed.Contains(x.RowGuid));
            }

            var rows = await source.OrderBy(key).Skip(skip).Take(take + 1).ToListAsync(ct);
            return rows.Cast<ISyncEntity>().ToList();
        }
    }
}
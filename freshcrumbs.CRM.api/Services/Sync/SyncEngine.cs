using System.Globalization;
using System.Net;
using System.Text.Json;
using freshcrumbs.CRM.domain.entities;
using freshcrumbs.CRM.infrastructure.data;
using freshcrumbs.CRM.infrastructure.services;
using Microsoft.EntityFrameworkCore;

namespace freshcrumbs.CRM.api.Services.Sync
{
    // LOCAL (desktop) side of the synchronization. One run = refresh access snapshot, push, pull.
    // Safe to interrupt at any point: the outbox row of a change is only marked Synced after the
    // cloud confirmed it, and the cloud ignores an OperationId it has already processed.
    public class SyncEngine
    {
        // Parents before children, so pulled foreign keys can always be resolved.
        private static readonly string[] PullOrder =
        {
            nameof(Branch), nameof(Customer), nameof(Product), nameof(BranchInventory), nameof(BranchAssignment),
            nameof(Promotion), nameof(CustomerDiscountEligibility),
            nameof(SalesTransaction), nameof(TransactionItem), nameof(LoyaltyTransaction),
            nameof(Feedback), nameof(Inquiry)
        };

        private const string PullCursorKey = "PullCursorUtc";
        private const string LastSyncKey = "LastSyncUtc";

        private readonly IServiceScopeFactory _scopes;
        private readonly CloudApiClient _cloud;
        private readonly LocalAccessCache _cache;
        private readonly SyncStatusService _status;
        private readonly SyncOptions _options;
        private readonly ILogger<SyncEngine> _log;
        private readonly SemaphoreSlim _runLock = new(1, 1);

        public SyncEngine(
            IServiceScopeFactory scopes,
            CloudApiClient cloud,
            LocalAccessCache cache,
            SyncStatusService status,
            SyncOptions options,
            ILogger<SyncEngine> log)
        {
            _scopes = scopes;
            _cloud = cloud;
            _cache = cache;
            _status = status;
            _options = options;
            _log = log;
        }

        public async Task RunOnceAsync(int companyId, CancellationToken ct)
        {
            // Only one synchronization at a time.
            if (!await _runLock.WaitAsync(0, ct))
            {
                _log.LogInformation("Sync for company {CompanyId} skipped: another run is still in progress.", companyId);
                return;
            }

            try
            {
                await RunCoreAsync(companyId, ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                // Never let a sync problem reach normal desktop operations.
                _log.LogWarning(ex, "Synchronization failed for company {CompanyId}", companyId);
                _status.Set("Error", error: ex.GetBaseException().Message);
            }
            finally
            {
                _runLock.Release();
            }
        }

        private async Task RunCoreAsync(int companyId, CancellationToken ct)
        {
            var token = _cache.Read(d => d.SyncTokens.TryGetValue(companyId, out var t) ? t : null);

            if (token == null || token.ExpiresAtUtc <= DateTime.UtcNow.AddMinutes(1))
            {
                _log.LogWarning("Sync stopped for company {CompanyId}: sync token {State}.", companyId, token == null ? "missing" : "expired at " + token.ExpiresAtUtc.ToString("O"));
                _status.Set("NeedsSignIn", error: "Sign in while connected to the internet to continue syncing.");
                return;
            }

            if (!await _cloud.PingAsync(ct))
            {
                _log.LogWarning("Sync stopped for company {CompanyId}: cloud ping failed.", companyId);
                _status.Set("Offline", online: false);
                return;
            }

            _status.Set("Syncing", online: true);

            // 1) Re-validate company / subscription / pending terms (restarts the offline grace period).
            var snapshot = await _cloud.GetSnapshotAsync(token.Token, companyId, ct);

            if (snapshot.Unreachable)
            {
                _log.LogWarning("Sync stopped for company {CompanyId}: snapshot unreachable ({Message}).", companyId, snapshot.Message);
                _status.Set("Offline", online: false);
                return;
            }

            if (!snapshot.Ok || snapshot.Data == null)
            {
                _log.LogWarning("Sync stopped for company {CompanyId}: snapshot rejected ({Status} {Message}).", companyId, snapshot.StatusCode, snapshot.Message);
                _status.Set("NeedsSignIn", error: snapshot.Message ?? "The cloud rejected the sync session. Sign in again.");
                return;
            }

            _cache.Update(doc =>
            {
                doc.Companies[companyId] = new CachedCompany
                {
                    Snapshot = snapshot.Data,
                    LastCloudValidatedUtc = DateTime.UtcNow
                };
                doc.LastSeenUtc = DateTime.UtcNow > doc.LastSeenUtc ? DateTime.UtcNow : doc.LastSeenUtc;
            });

            using var scope = _scopes.CreateScope();
            var factory = scope.ServiceProvider.GetRequiredService<ITenantDbContextFactory>();
            await using var db = await factory.CreateAsync(companyId);

            // 2) Push local changes.
            if (!await PushAsync(db, token.Token, companyId, ct))
            {
                _log.LogWarning("Sync stopped for company {CompanyId} during PUSH (see sync status error).", companyId);
                return;
            }

            // 3) Pull changes made elsewhere.
            if (!await PullAsync(db, token.Token, companyId, ct))
            {
                _log.LogWarning("Sync stopped for company {CompanyId} during PULL (see sync status error).", companyId);
                return;
            }

            await SetStateAsync(db, LastSyncKey, DateTime.UtcNow.ToString("O"), ct);
            _log.LogInformation("Sync completed for company {CompanyId}.", companyId);
            _status.MarkSynced();
        }

        // Returns false when the run must stop (offline / error already reported to the status).
        private async Task<bool> PushAsync(TenantCrmDbContext db, string token, int companyId, CancellationToken ct)
        {
            // Local rows are never modified while we only read them for sending.
            while (true)
            {
                var window = await db.SyncOutbox
                    .AsNoTracking()
                    .Where(o => o.Status == SyncOutboxStatus.Pending)
                    .OrderBy(o => o.Sequence)
                    .Take(_options.PushBatchGroups * 20)
                    .Select(o => o.GroupId)
                    .ToListAsync(ct);

                if (window.Count == 0)
                {
                    break;
                }

                var groupIds = window.Distinct().Take(_options.PushBatchGroups).ToList();

                var rows = await db.SyncOutbox
                    .Where(o => o.Status == SyncOutboxStatus.Pending && groupIds.Contains(o.GroupId))
                    .OrderBy(o => o.Sequence)
                    .ToListAsync(ct);

                var request = new PushRequest
                {
                    Operations = rows.Select(r => new PushOperationDto
                    {
                        OperationId = r.OperationId,
                        GroupId = r.GroupId,
                        EntityType = r.EntityType,
                        EntityRowGuid = r.EntityRowGuid,
                        Operation = r.Operation,
                        ChangedAtUtc = DateTime.SpecifyKind(r.ChangedAtUtc, DateTimeKind.Utc),
                        Payload = JsonDocument.Parse(r.Payload).RootElement.Clone()
                    }).ToList()
                };

                var response = await _cloud.PushAsync(token, companyId, request, ct);

                if (response.Unreachable)
                {
                    // Connection lost (possibly in the middle of the request). Nothing is lost: the rows stay
                    // Pending and the cloud answers "Duplicate" for anything it did receive.
                    _status.Set("Offline", online: false);
                    return false;
                }

                if (!response.Ok || response.Data == null)
                {
                    var message = response.Message ?? "The cloud did not accept the changes.";
                    _status.Set(response.StatusCode == HttpStatusCode.Unauthorized ? "NeedsSignIn" : "Error", error: message);
                    return false;
                }

                var byId = response.Data.Results.ToDictionary(r => r.OperationId);
                var anyFailed = false;

                foreach (var row in rows)
                {
                    if (!byId.TryGetValue(row.OperationId, out var result))
                    {
                        continue; // no answer for this row: stays Pending
                    }

                    switch (result.Result)
                    {
                        case PushResult.Applied:
                        case PushResult.Duplicate:
                        case PushResult.Skipped:
                            row.Status = SyncOutboxStatus.Synced;
                            row.SyncedAtUtc = DateTime.UtcNow;
                            row.LastError = result.Detail;
                            break;

                        case PushResult.Rejected:
                            row.Status = SyncOutboxStatus.Rejected;
                            row.LastError = result.Detail;
                            break;

                        default:
                            anyFailed = true;
                            row.Attempts++;
                            row.LastError = result.Detail;

                            if (row.Attempts >= _options.MaxAttempts)
                            {
                                row.Status = SyncOutboxStatus.Rejected;
                                row.LastError = $"Gave up after {row.Attempts} attempts: {result.Detail}";
                            }
                            break;
                    }
                }

                await db.SaveChangesAsync(ct);

                // No answer at all for this batch: stop instead of resending the same rows forever.
                if (rows.All(r => r.Status == SyncOutboxStatus.Pending) && !anyFailed)
                {
                    _status.Set("Error", error: "The cloud returned no result for the pending changes.");
                    return false;
                }

                if (anyFailed)
                {
                    // Keep order: later changes may depend on the one that failed. Try again next cycle.
                    _status.Set("Error", error: rows.FirstOrDefault(r => r.Status == SyncOutboxStatus.Pending)?.LastError);
                    return false;
                }
            }

            // Housekeeping: forget confirmed entries after two weeks.
            var cutoff = DateTime.UtcNow.AddDays(-14);
            await db.SyncOutbox
                .Where(o => o.Status == SyncOutboxStatus.Synced && o.SyncedAtUtc < cutoff)
                .ExecuteDeleteAsync(ct);

            return true;
        }

        private async Task<bool> PullAsync(TenantCrmDbContext db, string token, int companyId, CancellationToken ct)
        {
            DateTime? since = null;
            var stored = await GetStateAsync(db, PullCursorKey, ct);

            if (stored != null && DateTime.TryParse(stored, CultureInfo.InvariantCulture,
                    DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var parsed))
            {
                since = parsed;
            }

            DateTime? serverTime = null;
            var incomplete = false;

            // Pulled data is stored as-is: no new UpdatedAt, nothing written to the outbox.
            db.ApplyingSync = true;
            db.CaptureSyncChanges = false;

            try
            {
                foreach (var type in PullOrder)
                {
                    var skip = 0;

                    while (true)
                    {
                        var page = await _cloud.PullAsync(token, companyId, type, since, skip, _options.PullPageSize, ct);

                        if (page.Unreachable)
                        {
                            _status.Set("Offline", online: false);
                            return false;
                        }

                        if (!page.Ok || page.Data == null)
                        {
                            _status.Set(page.StatusCode == HttpStatusCode.Unauthorized ? "NeedsSignIn" : "Error",
                                error: page.Message ?? "Could not download changes.");
                            return false;
                        }

                        serverTime ??= page.Data.ServerTimeUtc;
                        _log.LogInformation("Pulled page of {Type}: {Count} item(s), hasMore={HasMore}.", type, page.Data.Items.Count, page.Data.HasMore);

                        foreach (var item in page.Data.Items)
                        {
                            if (!await ApplyPulledAsync(db, type, item, ct))
                            {
                                incomplete = true;
                            }
                        }

                        try
                        {
                            await db.SaveChangesAsync(ct);
                        }
                        catch (DbUpdateException ex)
                        {
                            _log.LogWarning(ex, "A pulled {Type} page could not be stored; it will be retried.", type);
                            db.ChangeTracker.Clear();
                            incomplete = true;
                        }

                        if (!page.Data.HasMore)
                        {
                            break;
                        }

                        skip += _options.PullPageSize;
                    }
                }
            }
            finally
            {
                db.ApplyingSync = false;
                db.CaptureSyncChanges = true;
            }

            if (incomplete)
            {
                _log.LogWarning("Pull finished INCOMPLETE: some pulled items were postponed or could not be stored. The pull cursor was not advanced.");
            }

            // The cursor only moves forward after a COMPLETE pull, so nothing can be skipped.
            if (!incomplete && serverTime != null)
            {
                await SetStateAsync(db, PullCursorKey, serverTime.Value.AddSeconds(-5).ToString("O"), ct);
            }

            return true;
        }

        // Returns false when the item could not be applied yet (so the cursor must not advance).
        private async Task<bool> ApplyPulledAsync(TenantCrmDbContext db, string type, PullItemDto item, CancellationToken ct)
        {
            // Local changes that were not sent yet win until they have been pushed (then the merged
            // cloud version comes back with the next pull).
            var pending = await db.SyncOutbox.AnyAsync(
                o => o.EntityRowGuid == item.RowGuid && o.Status == SyncOutboxStatus.Pending, ct);

            if (pending)
            {
                return true;
            }

            var op = new PushOperationDto
            {
                OperationId = Guid.NewGuid(),
                GroupId = Guid.NewGuid(),
                EntityType = type,
                EntityRowGuid = item.RowGuid,
                Operation = SyncOperationType.Upsert,
                ChangedAtUtc = DateTime.SpecifyKind(item.UpdatedAt, DateTimeKind.Utc),
                Payload = item.Payload
            };

            var (result, detail) = await CloudSyncApplier.UpsertAsync(db, op, absolute: true, ct);

            if (result == PushResult.Failed)
            {
                _log.LogWarning("Pulled {Type} {RowGuid} postponed: {Detail}", type, item.RowGuid, detail);
                return false;
            }

            return true;
        }

        private static async Task<string?> GetStateAsync(TenantCrmDbContext db, string key, CancellationToken ct)
        {
            return await db.SyncStates.AsNoTracking()
                .Where(s => s.Key == key)
                .Select(s => s.Value)
                .FirstOrDefaultAsync(ct);
        }

        private static async Task SetStateAsync(TenantCrmDbContext db, string key, string value, CancellationToken ct)
        {
            var state = await db.SyncStates.FirstOrDefaultAsync(s => s.Key == key, ct);

            if (state == null)
            {
                db.SyncStates.Add(new SyncState { Key = key, Value = value });
            }
            else
            {
                state.Value = value;
            }

            await db.SaveChangesAsync(ct);
        }
    }
}
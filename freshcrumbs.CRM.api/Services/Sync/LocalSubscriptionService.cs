using freshcrumbs.CRM.domain.entities;

namespace freshcrumbs.CRM.api.Services.Sync
{
    // Replaces SubscriptionService in Local mode. The rules are identical (same Subscription logic,
    // same feature keys); the data comes from the snapshot the cloud supplied at the last online
    // validation, so BASIC / STANDARD / PREMIUM restrictions are still enforced offline.
    public class LocalSubscriptionService : ISubscriptionService
    {
        private readonly LocalAccessCache _cache;

        public LocalSubscriptionService(LocalAccessCache cache)
        {
            _cache = cache;
        }

        public Task<Subscription?> GetCurrentAsync(int companyId)
        {
            var now = DateTime.UtcNow;
            var subs = LoadSubscriptions(companyId);

            var current = subs.Where(s => s.IsCurrent(now))
                .OrderByDescending(s => s.StartDate).ThenByDescending(s => s.SubscriptionId)
                .FirstOrDefault();

            return Task.FromResult(current);
        }

        public Task<EntitlementResult> CheckAccessAsync(int companyId, string? requiredFeature)
        {
            _cache.Touch();

            var grace = _cache.GetGrace(companyId);

            if (!grace.Valid)
            {
                return Task.FromResult(new EntitlementResult { Code = grace.Code, Message = grace.Message });
            }

            var now = DateTime.UtcNow;
            var snapshot = _cache.Read(d => d.Companies.TryGetValue(companyId, out var c) ? c.Snapshot : null);

            if (snapshot == null || !snapshot.IsActive)
            {
                return Task.FromResult(new EntitlementResult { Code = "CompanyInactive", Message = "This company account is not active." });
            }

            var all = LoadSubscriptions(companyId);
            var current = all.Where(s => s.IsCurrent(now))
                .OrderByDescending(s => s.StartDate).ThenByDescending(s => s.SubscriptionId)
                .FirstOrDefault();

            if (current == null)
            {
                var display = SubscriptionService.PickDisplay(all, now);

                if (display == null)
                {
                    return Task.FromResult(new EntitlementResult { Code = "NoSubscription", Message = "This company does not have a subscription. Please contact FreshCrumbs." });
                }

                return Task.FromResult(display.GetDisplayStatus(now) switch
                {
                    "Cancelled" => new EntitlementResult { Code = "SubscriptionCancelled", Display = display, Message = "This company's subscription has been cancelled. Please contact FreshCrumbs." },
                    "Scheduled" => new EntitlementResult { Code = "SubscriptionNotStarted", Display = display, Message = $"The subscription starts on {display.StartDate:yyyy-MM-dd}." },
                    _ => new EntitlementResult { Code = "SubscriptionExpired", Display = display, Message = $"The subscription expired on {display.EndDate:yyyy-MM-dd}. Please contact FreshCrumbs to renew." }
                });
            }

            if (current.Status == SubscriptionStatus.Suspended)
            {
                return Task.FromResult(new EntitlementResult
                {
                    Code = "SubscriptionSuspended",
                    Subscription = current,
                    Display = current,
                    Message = "This company's subscription is suspended. Please contact FreshCrumbs."
                });
            }

            if (requiredFeature != null && !current.HasFeature(requiredFeature))
            {
                return Task.FromResult(new EntitlementResult
                {
                    Code = "FeatureNotIncluded",
                    Subscription = current,
                    Display = current,
                    Message = $"Your current plan ({current.PlanName}) does not include {PlanFeatureKeys.DisplayName(requiredFeature)}."
                });
            }

            return Task.FromResult(new EntitlementResult { Allowed = true, Subscription = current, Display = current });
        }

        public Task<int> CountActiveUsersAsync(int companyId)
        {
            var count = _cache.Read(d => d.Companies.TryGetValue(companyId, out var c) ? c.Snapshot.ActiveUsers : 0);
            return Task.FromResult(count);
        }

        public async Task<string?> CheckUserAsync(string? userId, int companyId)
        {
            return (await CheckTenantUserAsync(userId, companyId)).Error;
        }

        public Task<TenantUserCheck> CheckTenantUserAsync(string? userId, int companyId)
        {
            var grace = _cache.GetGrace(companyId);

            if (!grace.Valid)
            {
                return Task.FromResult(new TenantUserCheck(grace.Message, null));
            }

            var user = _cache.Read(d => userId != null && d.Users.TryGetValue(userId, out var u) ? u : null);

            if (user == null || user.TenantId != companyId)
            {
                return Task.FromResult(new TenantUserCheck("This account does not belong to this company.", null));
            }

            if (_cache.IsUserInactive(companyId, user.Id))
            {
                return Task.FromResult(new TenantUserCheck("This account is inactive. Please contact your administrator.", null));
            }

            return Task.FromResult(new TenantUserCheck(null, user.Role));
        }

        // User administration and plan changes are platform/cloud operations.
        public Task<string?> CheckCanActivateUserAsync(int companyId) => throw CloudOnly();

        public Task<SubscriptionResult> ChangePlanAsync(int companyId, int planId, DateTime effectiveDate, string? reason, string changedBy) => throw CloudOnly();

        public Task<SubscriptionResult> RenewAsync(int companyId, string? reason, string changedBy) => throw CloudOnly();

        public Task<SubscriptionResult> CancelAsync(int companyId, DateTime effectiveDate, string? reason, string changedBy) => throw CloudOnly();

        public Task<SubscriptionResult> SetSuspendedAsync(int companyId, bool suspend, string changedBy) => throw CloudOnly();

        private List<Subscription> LoadSubscriptions(int companyId)
        {
            return _cache.Read(d => d.Companies.TryGetValue(companyId, out var c)
                    ? c.Snapshot.Subscriptions.Select(s => s.ToEntity()).ToList()
                    : new List<Subscription>());
        }

        private static NotSupportedException CloudOnly() =>
            new("This operation needs the cloud. Connect to the internet and use the cloud service.");
    }

    // Local mode: a pending Terms & Conditions version (from the last online validation) still blocks use.
    public class LocalTermsService : ITermsService
    {
        private readonly LocalAccessCache _cache;

        public LocalTermsService(LocalAccessCache cache)
        {
            _cache = cache;
        }

        public Task<TermsVersion?> GetCurrentAsync() => Task.FromResult<TermsVersion?>(null);

        public Task<TermsVersion?> GetPendingForCompanyAsync(int companyId)
        {
            var pending = _cache.Read(d => d.Companies.TryGetValue(companyId, out var c) ? c.Snapshot.PendingTermsVersion : null);

            TermsVersion? result = pending == null ? null : new TermsVersion { VersionNumber = pending.Value };
            return Task.FromResult(result);
        }
    }
}
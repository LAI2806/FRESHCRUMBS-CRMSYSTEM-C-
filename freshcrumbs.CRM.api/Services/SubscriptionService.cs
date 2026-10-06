using freshcrumbs.CRM.domain.entities;
using freshcrumbs.CRM.infrastructure.data;
using Microsoft.EntityFrameworkCore;

namespace freshcrumbs.CRM.api.Services
{
    public record SubscriptionResult(Subscription? Subscription, string? Error)
    {
        public bool Succeeded => Error == null;
    }

    public class EntitlementResult
    {
        public bool Allowed { get; set; }
        public string Code { get; set; } = "Allowed";
        public string Message { get; set; } = string.Empty;

        // The subscription currently in force (null when none).
        public Subscription? Subscription { get; set; }

        // The subscription to describe to the user (current, otherwise the most recent one).
        public Subscription? Display { get; set; }
    }

    public interface ISubscriptionService
    {
        Task<Subscription?> GetCurrentAsync(int companyId);
        Task<EntitlementResult> CheckAccessAsync(int companyId, string? requiredFeature);
        Task<int> CountActiveUsersAsync(int companyId);
        Task<string?> CheckUserAsync(string? userId, int companyId);
        Task<string?> CheckCanActivateUserAsync(int companyId);
        Task<SubscriptionResult> ChangePlanAsync(int companyId, int planId, DateTime effectiveDate, string? reason, string changedBy);
        Task<SubscriptionResult> RenewAsync(int companyId, string? reason, string changedBy);
        Task<SubscriptionResult> CancelAsync(int companyId, DateTime effectiveDate, string? reason, string changedBy);
        Task<SubscriptionResult> SetSuspendedAsync(int companyId, bool suspend, string changedBy);
    }

    public class SubscriptionService : ISubscriptionService
    {
        private const int RenewalWindowDays = 30;
        private const string ActiveUserStatus = "Active";

        private readonly MasterCrmDbContext _db;

        public SubscriptionService(MasterCrmDbContext db)
        {
            _db = db;
        }

        // Current if one is in force, else the latest that has started, else the earliest scheduled.
        public static Subscription? PickDisplay(IEnumerable<Subscription> subscriptions, DateTime now)
        {
            var list = subscriptions.ToList();

            return list.Where(s => s.IsCurrent(now))
                       .OrderByDescending(s => s.StartDate).ThenByDescending(s => s.SubscriptionId)
                       .FirstOrDefault()
                   ?? list.Where(s => s.StartDate <= now)
                       .OrderByDescending(s => s.StartDate).ThenByDescending(s => s.SubscriptionId)
                       .FirstOrDefault()
                   ?? list.OrderBy(s => s.StartDate).ThenBy(s => s.SubscriptionId)
                       .FirstOrDefault();
        }

        public async Task<Subscription?> GetCurrentAsync(int companyId)
        {
            var now = DateTime.UtcNow;

            return await _db.Subscriptions
                .Where(s => s.CompanyId == companyId
                    && (s.Status == SubscriptionStatus.Active || s.Status == SubscriptionStatus.Suspended)
                    && s.StartDate <= now
                    && s.EndDate > now)
                .OrderByDescending(s => s.StartDate).ThenByDescending(s => s.SubscriptionId)
                .FirstOrDefaultAsync();
        }

        public async Task<int> CountActiveUsersAsync(int companyId)
        {
            return await _db.Users.CountAsync(u => u.TenantId == companyId && u.Status == ActiveUserStatus);
        }

        // The token proves who the caller was at login; this confirms the account is still valid for this company.
        public async Task<string?> CheckUserAsync(string? userId, int companyId)
        {
            var user = await _db.Users.AsNoTracking()
                .Where(u => u.Id == userId)
                .Select(u => new { u.TenantId, u.Status })
                .FirstOrDefaultAsync();

            if (user == null || user.TenantId != companyId)
            {
                return "This account does not belong to this company.";
            }

            if (!string.Equals(user.Status, ActiveUserStatus, StringComparison.OrdinalIgnoreCase))
            {
                return "This account is inactive. Please contact your administrator.";
            }

            return null;
        }

        public async Task<EntitlementResult> CheckAccessAsync(int companyId, string? requiredFeature)
        {
            var now = DateTime.UtcNow;

            var company = await _db.Companies.AsNoTracking().FirstOrDefaultAsync(c => c.CompanyId == companyId);

            if (company == null || !company.IsActive)
            {
                return new EntitlementResult { Code = "CompanyInactive", Message = "This company account is not active." };
            }

            var current = await GetCurrentAsync(companyId);

            if (current == null)
            {
                var all = await _db.Subscriptions.AsNoTracking().Where(s => s.CompanyId == companyId).ToListAsync();
                var display = PickDisplay(all, now);

                if (display == null)
                {
                    return new EntitlementResult { Code = "NoSubscription", Message = "This company does not have a subscription. Please contact FreshCrumbs." };
                }

                var status = display.GetDisplayStatus(now);

                return status switch
                {
                    "Cancelled" => new EntitlementResult { Code = "SubscriptionCancelled", Display = display, Message = "This company's subscription has been cancelled. Please contact FreshCrumbs." },
                    "Scheduled" => new EntitlementResult { Code = "SubscriptionNotStarted", Display = display, Message = $"The subscription starts on {display.StartDate:yyyy-MM-dd}." },
                    _ => new EntitlementResult { Code = "SubscriptionExpired", Display = display, Message = $"The subscription expired on {display.EndDate:yyyy-MM-dd}. Please contact FreshCrumbs to renew." }
                };
            }

            if (current.Status == SubscriptionStatus.Suspended)
            {
                return new EntitlementResult
                {
                    Code = "SubscriptionSuspended",
                    Subscription = current,
                    Display = current,
                    Message = "This company's subscription is suspended. Please contact FreshCrumbs."
                };
            }

            if (requiredFeature != null && !current.HasFeature(requiredFeature))
            {
                return new EntitlementResult
                {
                    Code = "FeatureNotIncluded",
                    Subscription = current,
                    Display = current,
                    Message = $"Your current plan ({current.PlanName}) does not include {PlanFeatureKeys.DisplayName(requiredFeature)}."
                };
            }

            return new EntitlementResult { Allowed = true, Subscription = current, Display = current };
        }

        public async Task<string?> CheckCanActivateUserAsync(int companyId)
        {
            var access = await CheckAccessAsync(companyId, null);

            if (!access.Allowed)
            {
                return $"Users cannot be added or reactivated: {access.Message}";
            }

            var activeUsers = await CountActiveUsersAsync(companyId);
            var max = access.Subscription!.MaxUsers;

            if (activeUsers >= max)
            {
                return $"User limit reached: the current plan allows {max} active users and this company already has {activeUsers}.";
            }

            return null;
        }

        public async Task<SubscriptionResult> ChangePlanAsync(int companyId, int planId, DateTime effectiveDate, string? reason, string changedBy)
        {
            var now = DateTime.UtcNow;

            if (!await _db.Companies.AnyAsync(c => c.CompanyId == companyId))
            {
                return Fail("Company not found.");
            }

            var plan = await _db.Plans.AsNoTracking().Include(p => p.Features).FirstOrDefaultAsync(p => p.PlanId == planId);

            if (plan == null)
            {
                return Fail("Plan not found.");
            }

            if (!plan.IsActive)
            {
                return Fail("Only active plans can be assigned.");
            }

            var dateError = ResolveStart(effectiveDate, now, out var start);

            if (dateError != null)
            {
                return Fail(dateError);
            }

            if (await HasScheduledAsync(companyId, now))
            {
                return Fail("A scheduled subscription change already exists for this company. Wait until it takes effect before making another change.");
            }

            var current = await GetCurrentAsync(companyId);

            if (current?.Status == SubscriptionStatus.Suspended)
            {
                return Fail("The subscription is suspended. Reactivate it before changing the plan.");
            }

            if (current != null && current.PlanId == plan.PlanId)
            {
                return Fail("The company is already on this plan. Use Renew to extend it.");
            }

            var activeUsers = await CountActiveUsersAsync(companyId);

            if (activeUsers > plan.MaxUsers)
            {
                return Fail($"This company has {activeUsers} active users, which exceeds the {plan.MaxUsers} allowed by '{plan.DisplayName}'. Deactivate users first or choose a larger plan.");
            }

            var previous = current ?? await GetLatestStartedAsync(companyId, now);

            SubscriptionChangeType changeType;

            if (current == null)
            {
                changeType = SubscriptionChangeType.New;
            }
            else
            {
                var newMonthly = MonthlyPrice(plan.Price, plan.BillingCycle);
                var oldMonthly = MonthlyPrice(current.Price, current.BillingCycle);

                changeType = newMonthly > oldMonthly || (newMonthly == oldMonthly && plan.MaxUsers >= current.MaxUsers)
                    ? SubscriptionChangeType.Upgrade
                    : SubscriptionChangeType.Downgrade;
            }

            var featureKeys = plan.Features.Select(f => f.FeatureKey).ToList();

            if (plan.BranchingEnabled)
            {
                featureKeys.Add(PlanFeatureKeys.Branching);
            }

            var subscription = new Subscription
            {
                CompanyId = companyId,
                PlanId = plan.PlanId,
                PlanCode = plan.PlanCode,
                PlanName = plan.DisplayName,
                Price = plan.Price,
                BillingCycle = plan.BillingCycle,
                Features = Subscription.JoinFeatures(featureKeys),
                MaxUsers = plan.MaxUsers,
                BranchingEnabled = plan.BranchingEnabled,
                MaxBranches = plan.BranchingEnabled ? plan.MaxBranches : null,
                StartDate = start,
                EndDate = Subscription.AddCycle(start, plan.BillingCycle),
                Status = SubscriptionStatus.Active,
                ChangeType = changeType,
                Reason = Clean(reason),
                ChangedBy = changedBy,
                PreviousSubscriptionId = previous?.SubscriptionId
            };

            // Close the superseded record at the effective date. Its plan terms are never touched.
            if (current != null && current.EndDate > start)
            {
                current.EndDate = start;
            }

            _db.Subscriptions.Add(subscription);
            await _db.SaveChangesAsync();

            return new SubscriptionResult(subscription, null);
        }

        public async Task<SubscriptionResult> RenewAsync(int companyId, string? reason, string changedBy)
        {
            var now = DateTime.UtcNow;

            if (await HasScheduledAsync(companyId, now))
            {
                return Fail("A scheduled subscription change already exists for this company.");
            }

            var current = await GetCurrentAsync(companyId);

            if (current?.Status == SubscriptionStatus.Suspended)
            {
                return Fail("The subscription is suspended. Reactivate it before renewing.");
            }

            var latest = current ?? await GetLatestStartedAsync(companyId, now);

            if (latest == null)
            {
                return Fail("This company has no subscription to renew. Assign a plan instead.");
            }

            if (latest.Status == SubscriptionStatus.Cancelled)
            {
                return Fail("A cancelled subscription cannot be renewed. Assign a plan instead.");
            }

            if (latest.PlanCode == PlatformConstants.LegacyPlanCode)
            {
                return Fail("The backfilled legacy subscription cannot be renewed. Assign a real plan instead.");
            }

            if (latest.EndDate > now.AddDays(RenewalWindowDays))
            {
                return Fail($"Renewal is only available within {RenewalWindowDays} days of the expiry date ({latest.EndDate:yyyy-MM-dd}).");
            }

            var start = latest.EndDate > now ? latest.EndDate : now;

            var renewal = CopyTerms(latest);
            renewal.StartDate = start;
            renewal.EndDate = Subscription.AddCycle(start, latest.BillingCycle);
            renewal.Status = SubscriptionStatus.Active;
            renewal.ChangeType = SubscriptionChangeType.Renewal;
            renewal.Reason = Clean(reason);
            renewal.ChangedBy = changedBy;
            renewal.PreviousSubscriptionId = latest.SubscriptionId;

            _db.Subscriptions.Add(renewal);
            await _db.SaveChangesAsync();

            return new SubscriptionResult(renewal, null);
        }

        public async Task<SubscriptionResult> CancelAsync(int companyId, DateTime effectiveDate, string? reason, string changedBy)
        {
            var now = DateTime.UtcNow;

            var dateError = ResolveStart(effectiveDate, now, out var start);

            if (dateError != null)
            {
                return Fail(dateError);
            }

            if (await HasScheduledAsync(companyId, now))
            {
                return Fail("A scheduled subscription change already exists for this company.");
            }

            var current = await GetCurrentAsync(companyId);

            if (current == null)
            {
                return Fail("This company has no active subscription to cancel.");
            }

            var cancellation = CopyTerms(current);
            cancellation.StartDate = start;
            cancellation.EndDate = start;
            cancellation.Status = SubscriptionStatus.Cancelled;
            cancellation.ChangeType = SubscriptionChangeType.Cancelled;
            cancellation.Reason = Clean(reason);
            cancellation.ChangedBy = changedBy;
            cancellation.PreviousSubscriptionId = current.SubscriptionId;

            if (current.EndDate > start)
            {
                current.EndDate = start;
            }

            _db.Subscriptions.Add(cancellation);
            await _db.SaveChangesAsync();

            return new SubscriptionResult(cancellation, null);
        }

        public async Task<SubscriptionResult> SetSuspendedAsync(int companyId, bool suspend, string changedBy)
        {
            var current = await GetCurrentAsync(companyId);

            if (current == null)
            {
                return Fail("This company has no subscription in force.");
            }

            if (suspend && current.Status == SubscriptionStatus.Suspended)
            {
                return Fail("The subscription is already suspended.");
            }

            if (!suspend && current.Status != SubscriptionStatus.Suspended)
            {
                return Fail("The subscription is not suspended.");
            }

            current.Status = suspend ? SubscriptionStatus.Suspended : SubscriptionStatus.Active;
            await _db.SaveChangesAsync();

            return new SubscriptionResult(current, null);
        }

        private async Task<Subscription?> GetLatestStartedAsync(int companyId, DateTime now)
        {
            return await _db.Subscriptions
                .Where(s => s.CompanyId == companyId && s.StartDate <= now)
                .OrderByDescending(s => s.StartDate).ThenByDescending(s => s.SubscriptionId)
                .FirstOrDefaultAsync();
        }

        private async Task<bool> HasScheduledAsync(int companyId, DateTime now)
        {
            return await _db.Subscriptions.AnyAsync(s => s.CompanyId == companyId && s.StartDate > now);
        }

        private static string? ResolveStart(DateTime effectiveDate, DateTime now, out DateTime start)
        {
            var date = effectiveDate.Date;
            start = now;

            if (date < now.Date.AddDays(-1))
            {
                return "The effective date cannot be in the past.";
            }

            if (date > now.Date.AddDays(365))
            {
                return "The effective date cannot be more than 365 days ahead.";
            }

            start = date <= now.Date ? now : date;
            return null;
        }

        public static decimal MonthlyPrice(decimal price, BillingCycle cycle) => cycle switch
        {
            BillingCycle.Weekly => price * 52m / 12m,
            BillingCycle.Yearly => price / 12m,
            _ => price
        };

        private static Subscription CopyTerms(Subscription source) => new()
        {
            CompanyId = source.CompanyId,
            PlanId = source.PlanId,
            PlanCode = source.PlanCode,
            PlanName = source.PlanName,
            Price = source.Price,
            BillingCycle = source.BillingCycle,
            Features = source.Features,
            MaxUsers = source.MaxUsers,
            BranchingEnabled = source.BranchingEnabled,
            MaxBranches = source.MaxBranches
        };

        private static string? Clean(string? reason)
        {
            var trimmed = reason?.Trim();
            return string.IsNullOrEmpty(trimmed) ? null : trimmed;
        }

        private static SubscriptionResult Fail(string message) => new(null, message);
    }
}
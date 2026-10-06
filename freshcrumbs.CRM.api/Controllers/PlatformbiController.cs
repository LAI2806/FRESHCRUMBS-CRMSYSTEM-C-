using System.Globalization;
using freshcrumbs.CRM.api.Services;
using freshcrumbs.CRM.domain.entities;
using freshcrumbs.CRM.infrastructure.data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace freshcrumbs.CRM.api.Controllers
{
    // Platform-level BI: how the FreshCrumbs platform is performing.
    // This is separate from the tenant Reports/BI (api/tenant/{companyId}/reports).
    [ApiController]
    [Authorize(Roles = PlatformRoles.SuperAdmin)]
    [Route("api/platform/bi")]
    public class PlatformBiController : ControllerBase
    {
        private const int ExpiringSoonDays = 30;

        private readonly MasterCrmDbContext _db;
        private readonly ITermsService _terms;

        public PlatformBiController(MasterCrmDbContext db, ITermsService terms)
        {
            _db = db;
            _terms = terms;
        }

        [HttpGet("overview")]
        public async Task<IActionResult> GetOverview([FromQuery] int days = 30)
        {
            if (days != 7 && days != 30 && days != 90 && days != 365)
            {
                days = 30;
            }

            var now = DateTime.UtcNow;

            var companies = await _db.Companies.AsNoTracking().ToListAsync();
            var subscriptions = await _db.Subscriptions.AsNoTracking().ToListAsync();
            var planStates = await _db.Plans.AsNoTracking().Select(p => p.IsActive).ToListAsync();

            var userCounts = (await _db.Users.AsNoTracking()
                    .Where(u => u.TenantId != null && u.Status == "Active")
                    .GroupBy(u => u.TenantId)
                    .Select(g => new { CompanyId = g.Key, Count = g.Count() })
                    .ToListAsync())
                .ToDictionary(x => x.CompanyId!.Value, x => x.Count);

            var byCompany = subscriptions.GroupBy(s => s.CompanyId).ToDictionary(g => g.Key, g => g.ToList());
            var companyById = companies.ToDictionary(c => c.CompanyId);

            var displays = companies
                .Select(c => new
                {
                    Company = c,
                    Display = byCompany.TryGetValue(c.CompanyId, out var list)
                        ? SubscriptionService.PickDisplay(list, now)
                        : null
                })
                .ToList();

            int CountStatus(string status) => displays.Count(x => x.Display?.GetDisplayStatus(now) == status);

            var inForce = displays
                .Where(x => x.Display != null && x.Display.IsCurrent(now))
                .Select(x => new { x.Company, Subscription = x.Display! })
                .ToList();

            var planDistribution = inForce
                .GroupBy(x => x.Subscription.PlanCode)
                .Select(g => new
                {
                    PlanCode = g.Key,
                    PlanName = g.OrderByDescending(x => x.Subscription.SubscriptionId).First().Subscription.PlanName,
                    Count = g.Count()
                })
                .OrderByDescending(x => x.Count)
                .ThenBy(x => x.PlanName)
                .ToList();

            // Backfilled (SYSTEM) records are bookkeeping, not real subscription changes.
            var since = now.AddDays(-days);
            var changes = subscriptions
                .Where(s => s.CreatedAt >= since && s.ChangedBy != PlatformConstants.SystemUser)
                .ToList();

            var changesByType = Enum.GetValues<SubscriptionChangeType>()
                .Select(type => new { ChangeType = type.ToString(), Count = changes.Count(c => c.ChangeType == type) })
                .ToList();

            var recentChanges = changes
                .OrderByDescending(c => c.CreatedAt)
                .ThenByDescending(c => c.SubscriptionId)
                .Take(10)
                .Select(c => new
                {
                    CompanyCode = companyById.TryGetValue(c.CompanyId, out var company) ? company.CompanyCode : "",
                    CompanyName = company?.CompanyName ?? "",
                    ChangeType = c.ChangeType.ToString(),
                    c.PlanName,
                    EffectiveDate = Utc(c.StartDate),
                    c.ChangedBy,
                    c.Reason
                })
                .ToList();

            var userUsage = inForce
                .Select(x =>
                {
                    var active = userCounts.TryGetValue(x.Company.CompanyId, out var count) ? count : 0;
                    var max = x.Subscription.MaxUsers;

                    return new
                    {
                        x.Company.CompanyId,
                        x.Company.CompanyCode,
                        x.Company.CompanyName,
                        x.Subscription.PlanName,
                        ActiveUsers = active,
                        MaxUsers = max,
                        PercentUsed = max > 0 ? (int)Math.Round(active * 100.0 / max) : 0
                    };
                })
                .OrderByDescending(x => x.PercentUsed)
                .ThenBy(x => x.CompanyName)
                .ToList();

            var expiringSoon = inForce
                .Where(x => x.Subscription.EndDate <= now.AddDays(ExpiringSoonDays))
                .OrderBy(x => x.Subscription.EndDate)
                .Select(x => new
                {
                    x.Company.CompanyCode,
                    x.Company.CompanyName,
                    x.Subscription.PlanName,
                    EndDate = Utc(x.Subscription.EndDate),
                    DaysLeft = (int)Math.Ceiling((x.Subscription.EndDate - now).TotalDays)
                })
                .ToList();

            var firstOfMonth = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);

            var months = Enumerable.Range(0, 12)
                .Select(i => firstOfMonth.AddMonths(i - 11))
                .Select(start =>
                {
                    var end = start.AddMonths(1);
                    var asOf = end > now ? now : end;

                    var activeAtMonth = subscriptions
                        .Where(s => (s.Status == SubscriptionStatus.Active || s.Status == SubscriptionStatus.Suspended)
                            && s.StartDate <= asOf && s.EndDate > asOf)
                        .Select(s => s.CompanyId)
                        .Distinct()
                        .Count();

                    var startedByMonth = subscriptions
                        .Where(s => s.StartDate <= asOf)
                        .Select(s => s.CompanyId)
                        .Distinct()
                        .Count();

                    return new
                    {
                        Month = start.ToString("MMM yyyy", CultureInfo.InvariantCulture),
                        NewTenants = companies.Count(c => c.CreatedAt >= start && c.CreatedAt < end),
                        TotalTenants = companies.Count(c => c.CreatedAt < asOf),
                        ActiveSubscriptions = activeAtMonth,
                        InactiveSubscriptions = Math.Max(0, startedByMonth - activeAtMonth)
                    };
                })
                .ToList();

            var planPopularity = subscriptions
                .Where(s => s.ChangedBy != PlatformConstants.SystemUser
                    && (s.ChangeType == SubscriptionChangeType.New
                        || s.ChangeType == SubscriptionChangeType.Upgrade
                        || s.ChangeType == SubscriptionChangeType.Downgrade))
                .GroupBy(s => s.PlanCode)
                .Select(g => new
                {
                    PlanCode = g.Key,
                    PlanName = g.OrderByDescending(s => s.SubscriptionId).First().PlanName,
                    Count = g.Count()
                })
                .OrderByDescending(x => x.Count)
                .ThenBy(x => x.PlanName)
                .ToList();

            var newSubscribers = subscriptions
                .Where(s => s.ChangeType == SubscriptionChangeType.New && s.ChangedBy != PlatformConstants.SystemUser)
                .GroupBy(s => s.CompanyId)
                .Count(g => g.Min(s => s.CreatedAt) >= since);

            var monthlyRecurringValue = Math.Round(
                displays
                    .Where(x => x.Display != null && x.Display.GetDisplayStatus(now) == "Active")
                    .Sum(x => SubscriptionService.MonthlyPrice(x.Display!.Price, x.Display.BillingCycle)),
                2);

            object? termsSummary = null;
            var currentTerms = await _terms.GetCurrentAsync();

            if (currentTerms != null)
            {
                var acceptedIds = await _db.TermsAcceptances.AsNoTracking()
                    .Where(a => a.TermsVersionId == currentTerms.TermsVersionId)
                    .Select(a => a.CompanyId)
                    .ToListAsync();

                termsSummary = new
                {
                    currentTerms.VersionNumber,
                    currentTerms.Title,
                    currentTerms.RequiresAcceptance,
                    AcceptedCompanies = acceptedIds.Count,
                    PendingCompanies = currentTerms.RequiresAcceptance
                        ? companies.Count(c => c.IsActive && !acceptedIds.Contains(c.CompanyId))
                        : 0
                };
            }

            return Ok(new
            {
                Days = days,
                TotalCompanies = companies.Count,
                TotalSubscribers = byCompany.Count,
                NewSubscribers = newSubscribers,
                MonthlyRecurringValue = monthlyRecurringValue,
                Months = months,
                PlanPopularity = planPopularity,
                ActiveCompanies = companies.Count(c => c.IsActive),
                SubscriptionStatus = new
                {
                    Active = CountStatus("Active"),
                    Expired = CountStatus("Expired"),
                    Cancelled = CountStatus("Cancelled"),
                    Suspended = CountStatus("Suspended"),
                    Scheduled = CountStatus("Scheduled"),
                    NoSubscription = displays.Count(x => x.Display == null)
                },
                ActivePlans = planStates.Count(active => active),
                InactivePlans = planStates.Count(active => !active),
                PlanDistribution = planDistribution,
                ChangesByType = changesByType,
                RecentChanges = recentChanges,
                UserUsage = userUsage,
                ExpiringSoon = expiringSoon,
                Terms = termsSummary
            });
        }

        private static DateTime Utc(DateTime value) => DateTime.SpecifyKind(value, DateTimeKind.Utc);
    }
}
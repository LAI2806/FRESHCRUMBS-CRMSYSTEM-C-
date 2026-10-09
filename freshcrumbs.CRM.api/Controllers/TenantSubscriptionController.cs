using freshcrumbs.CRM.api.Authorization;
using freshcrumbs.CRM.api.Services;
using freshcrumbs.CRM.api.Services.Sync;
using freshcrumbs.CRM.infrastructure.data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace freshcrumbs.CRM.api.Controllers
{
    [ApiController]
    [Route("api/tenant/{companyId:int}/subscription")]
    public class TenantSubscriptionController : ControllerBase
    {
        private readonly ISubscriptionService _subscriptions;

        public TenantSubscriptionController(ISubscriptionService subscriptions)
        {
            _subscriptions = subscriptions;
        }

        // Access (authentication + company match) is enforced by TenantAccessFilter.
        [HttpGet]
        public async Task<IActionResult> Get(int companyId)
        {
            var access = await _subscriptions.CheckAccessAsync(companyId, null);
            var subscription = access.Display;
            var activeUsers = await _subscriptions.CountActiveUsersAsync(companyId);

            // Set by TenantAccessFilter from the account stored in the database.
            var role = HttpContext.Items[TenantAccessFilter.RoleItemKey] as string;
            var features = access.Allowed ? subscription!.FeatureList : new List<string>();

            return Ok(new
            {
                HasAccess = access.Allowed,
                access.Code,
                access.Message,
                PlanCode = subscription?.PlanCode,
                PlanName = subscription?.PlanName,
                EndDate = subscription == null ? (DateTime?)null : DateTime.SpecifyKind(subscription.EndDate, DateTimeKind.Utc),
                Features = features,
                Role = role,
                // What this role may do under this plan (role AND feature). The API still enforces every call.
                Permissions = access.Allowed ? TenantPermissions.Effective(role, features) : new List<string>(),
                MaxUsers = subscription?.MaxUsers,
                ActiveUsers = activeUsers,
                BranchingEnabled = subscription?.BranchingEnabled ?? false,
                MaxBranches = subscription?.MaxBranches
            });
        }

        // Cloud: what a desktop needs to enforce the plan, the company state and pending terms while offline.
        // It only describes the caller's own company (TenantAccessFilter already matched the company).
        [HttpGet("snapshot")]
        public async Task<IActionResult> Snapshot(
            int companyId,
            [FromServices] MasterCrmDbContext masterDb,
            [FromServices] ITermsService terms)
        {
            var company = await masterDb.Companies.AsNoTracking()
                .FirstOrDefaultAsync(c => c.CompanyId == companyId);

            if (company == null)
            {
                return NotFound();
            }

            var subscriptions = await masterDb.Subscriptions.AsNoTracking()
                .Where(s => s.CompanyId == companyId)
                .OrderByDescending(s => s.StartDate)
                .Take(12)
                .ToListAsync();

            var pending = await terms.GetPendingForCompanyAsync(companyId);

            var users = await masterDb.Users.AsNoTracking()
                .Where(u => u.TenantId == companyId)
                .OrderBy(u => u.UserName)
                .Select(u => new { u.Id, u.UserName, u.FirstName, u.LastName, u.Role, u.Status, u.Email })
                .ToListAsync();

            return Ok(new CompanySnapshotDto
            {
                CompanyId = company.CompanyId,
                CompanyCode = company.CompanyCode,
                CompanyName = company.CompanyName,
                IsActive = company.IsActive,
                Subscriptions = subscriptions.Select(SubscriptionDto.From).ToList(),
                ActiveUsers = await _subscriptions.CountActiveUsersAsync(companyId),
                PendingTermsVersion = pending?.VersionNumber,
                Users = users.Select(u => new CompanyUserDto
                {
                    Id = u.Id,
                    UserName = u.UserName ?? string.Empty,
                    FullName = $"{u.FirstName} {u.LastName}".Trim(),
                    Role = u.Role,
                    Status = u.Status,
                    FirstName = u.FirstName,
                    LastName = u.LastName,
                    Email = u.Email ?? string.Empty
                }).ToList()
            });
        }
    }
}
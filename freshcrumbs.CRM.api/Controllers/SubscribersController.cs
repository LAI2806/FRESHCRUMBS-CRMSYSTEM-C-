using System.Text.RegularExpressions;
using freshcrumbs.CRM.api.Services;
using freshcrumbs.CRM.domain.entities;
using freshcrumbs.CRM.infrastructure.data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace freshcrumbs.CRM.api.Controllers
{
    [ApiController]
    [Authorize(Roles = PlatformRoles.SuperAdmin)]
    [Route("api/platform/subscribers")]
    public class SubscribersController : ControllerBase
    {
        private static readonly Regex CompanyCodePattern = new("^[A-Z0-9][A-Z0-9_-]{1,49}$", RegexOptions.Compiled);
        private static readonly Regex EmailPattern = new(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.Compiled);
        private static readonly Regex PhonePattern = new(@"^[0-9+\-\s()]{7,20}$", RegexOptions.Compiled);

        private readonly MasterCrmDbContext _db;
        private readonly ISubscriptionService _subscriptions;

        public SubscribersController(MasterCrmDbContext db, ISubscriptionService subscriptions)
        {
            _db = db;
            _subscriptions = subscriptions;
        }

        public class RegisterRequest
        {
            public string CompanyCode { get; set; } = string.Empty;
            public string CompanyName { get; set; } = string.Empty;
            public string BusinessAddress { get; set; } = string.Empty;
            public string ContactNo { get; set; } = string.Empty;
            public string Email { get; set; } = string.Empty;
            public int PlanId { get; set; }
            public DateTime StartDate { get; set; }
        }

        public class ChangePlanRequest
        {
            public int PlanId { get; set; }
            public DateTime EffectiveDate { get; set; }
            public string? Reason { get; set; }
        }

        public class RenewRequest
        {
            public string? Reason { get; set; }
        }

        public class CancelRequest
        {
            public DateTime EffectiveDate { get; set; }
            public string? Reason { get; set; }
        }

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var now = DateTime.UtcNow;

            var companies = await _db.Companies.AsNoTracking().OrderBy(c => c.CompanyName).ToListAsync();
            var subscriptions = await _db.Subscriptions.AsNoTracking().ToListAsync();

            var userCounts = (await _db.Users.AsNoTracking()
                    .Where(u => u.TenantId != null && u.Status == "Active")
                    .GroupBy(u => u.TenantId)
                    .Select(g => new { CompanyId = g.Key, Count = g.Count() })
                    .ToListAsync())
                .ToDictionary(x => x.CompanyId!.Value, x => x.Count);

            var bySubscriber = subscriptions.GroupBy(s => s.CompanyId).ToDictionary(g => g.Key, g => g.ToList());

            var databases = (await _db.CompanyDatabases.AsNoTracking().ToListAsync())
                .GroupBy(d => d.CompanyId)
                .ToDictionary(
                    g => g.Key,
                    g => g.OrderByDescending(d => d.IsActive).ThenByDescending(d => d.CompanyDatabaseId).First());

            var items = companies.Select(company =>
            {
                databases.TryGetValue(company.CompanyId, out var database);

                var display = bySubscriber.TryGetValue(company.CompanyId, out var list)
                    ? SubscriptionService.PickDisplay(list, now)
                    : null;

                return new
                {
                    company.CompanyId,
                    company.CompanyCode,
                    company.CompanyName,
                    company.Email,
                    company.ContactNo,
                    company.IsActive,
                    PlanCode = display?.PlanCode,
                    PlanName = display?.PlanName,
                    Status = display?.GetDisplayStatus(now) ?? "No Subscription",
                    EndDate = display == null ? (DateTime?)null : Utc(display.EndDate),
                    ActiveUsers = userCounts.TryGetValue(company.CompanyId, out var count) ? count : 0,
                    MaxUsers = display?.MaxUsers,
                    DatabaseServer = database?.ServerName,
                    DatabaseName = database?.DatabaseName,
                    DatabaseCredentialKey = database?.CredentialKey,
                    DatabaseStatus = database == null ? "Not provisioned" : database.IsActive ? "Active" : "Inactive"
                };
            });

            return Ok(items);
        }

        [HttpGet("{companyId:int}")]
        public async Task<IActionResult> GetById(int companyId)
        {
            var detail = await BuildDetailAsync(companyId);

            if (detail == null)
            {
                return NotFound(new { message = $"Company with id {companyId} not found." });
            }

            return Ok(detail);
        }

        [HttpPost]
        public async Task<IActionResult> Register(RegisterRequest request)
        {
            var error = ValidateRegistration(request, out var code);

            if (error != null)
            {
                return BadRequest(new { message = error });
            }

            if (await _db.Companies.AnyAsync(c => c.CompanyCode == code))
            {
                return Conflict(new { message = $"Company code '{code}' is already in use." });
            }

            await using var transaction = await _db.Database.BeginTransactionAsync();

            var company = new Company
            {
                CompanyCode = code,
                CompanyName = request.CompanyName.Trim(),
                BusinessAddress = request.BusinessAddress.Trim(),
                ContactNo = request.ContactNo.Trim(),
                Email = request.Email.Trim(),
                Status = "Active",
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            _db.Companies.Add(company);
            await _db.SaveChangesAsync();

            var result = await _subscriptions.ChangePlanAsync(
                company.CompanyId, request.PlanId, request.StartDate, "Initial registration", ChangedBy());

            if (!result.Succeeded)
            {
                await transaction.RollbackAsync();
                return BadRequest(new { message = result.Error });
            }

            await transaction.CommitAsync();

            var detail = await BuildDetailAsync(company.CompanyId);
            return Created($"api/platform/subscribers/{company.CompanyId}", detail);
        }

        [HttpPost("{companyId:int}/change-plan")]
        public async Task<IActionResult> ChangePlan(int companyId, ChangePlanRequest request)
        {
            var reasonError = ValidateReason(request.Reason);

            if (reasonError != null)
            {
                return BadRequest(new { message = reasonError });
            }

            var result = await _subscriptions.ChangePlanAsync(
                companyId, request.PlanId, request.EffectiveDate, request.Reason, ChangedBy());

            return await ToResponseAsync(companyId, result);
        }

        [HttpPost("{companyId:int}/renew")]
        public async Task<IActionResult> Renew(int companyId, RenewRequest request)
        {
            var reasonError = ValidateReason(request.Reason);

            if (reasonError != null)
            {
                return BadRequest(new { message = reasonError });
            }

            return await ToResponseAsync(companyId, await _subscriptions.RenewAsync(companyId, request.Reason, ChangedBy()));
        }

        [HttpPost("{companyId:int}/cancel")]
        public async Task<IActionResult> Cancel(int companyId, CancelRequest request)
        {
            var reasonError = ValidateReason(request.Reason);

            if (reasonError != null)
            {
                return BadRequest(new { message = reasonError });
            }

            return await ToResponseAsync(
                companyId,
                await _subscriptions.CancelAsync(companyId, request.EffectiveDate, request.Reason, ChangedBy()));
        }

        [HttpPost("{companyId:int}/suspend")]
        public async Task<IActionResult> Suspend(int companyId)
        {
            return await ToResponseAsync(companyId, await _subscriptions.SetSuspendedAsync(companyId, true, ChangedBy()));
        }

        [HttpPost("{companyId:int}/reactivate")]
        public async Task<IActionResult> Reactivate(int companyId)
        {
            return await ToResponseAsync(companyId, await _subscriptions.SetSuspendedAsync(companyId, false, ChangedBy()));
        }

        private async Task<IActionResult> ToResponseAsync(int companyId, SubscriptionResult result)
        {
            if (!result.Succeeded)
            {
                return result.Error == "Company not found."
                    ? NotFound(new { message = result.Error })
                    : BadRequest(new { message = result.Error });
            }

            return Ok(await BuildDetailAsync(companyId));
        }

        private async Task<object?> BuildDetailAsync(int companyId)
        {
            var company = await _db.Companies.AsNoTracking().FirstOrDefaultAsync(c => c.CompanyId == companyId);

            if (company == null)
            {
                return null;
            }

            var now = DateTime.UtcNow;

            var history = await _db.Subscriptions.AsNoTracking()
                .Where(s => s.CompanyId == companyId)
                .OrderByDescending(s => s.StartDate).ThenByDescending(s => s.SubscriptionId)
                .ToListAsync();

            var display = SubscriptionService.PickDisplay(history, now);
            int? currentId = display != null && display.IsCurrent(now) ? display.SubscriptionId : null;

            return new
            {
                Company = new
                {
                    company.CompanyId,
                    company.CompanyCode,
                    company.CompanyName,
                    company.BusinessAddress,
                    company.ContactNo,
                    company.Email,
                    company.Status,
                    company.IsActive,
                    CreatedAt = Utc(company.CreatedAt)
                },
                TenantDatabaseProvisioned = await _db.CompanyDatabases.AnyAsync(x => x.CompanyId == companyId && x.IsActive),
                TenantDatabase = await _db.CompanyDatabases.AsNoTracking()
                    .Where(x => x.CompanyId == companyId)
                    .OrderByDescending(x => x.IsActive).ThenByDescending(x => x.CompanyDatabaseId)
                    .Select(x => new { x.ServerName, x.DatabaseName, x.CredentialKey, x.IsActive })
                    .FirstOrDefaultAsync(),
                ActiveUsers = await _subscriptions.CountActiveUsersAsync(companyId),
                MaxUsers = display?.MaxUsers,
                CurrentSubscription = display == null ? null : ToDto(display, now, currentId),
                History = history.Select(s => ToDto(s, now, currentId)).ToList()
            };
        }

        private static object ToDto(Subscription s, DateTime now, int? currentId)
        {
            return new
            {
                s.SubscriptionId,
                s.PlanId,
                s.PlanCode,
                s.PlanName,
                s.Price,
                BillingCycle = s.BillingCycle.ToString(),
                Features = s.FeatureList,
                s.MaxUsers,
                s.BranchingEnabled,
                s.MaxBranches,
                StartDate = Utc(s.StartDate),
                EndDate = Utc(s.EndDate),
                Status = s.GetDisplayStatus(now),
                ChangeType = s.ChangeType.ToString(),
                s.Reason,
                s.ChangedBy,
                s.PreviousSubscriptionId,
                CreatedAt = Utc(s.CreatedAt),
                IsCurrent = s.SubscriptionId == currentId
            };
        }

        private string ChangedBy() => User.Identity?.Name ?? "unknown";

        private static DateTime Utc(DateTime value) => DateTime.SpecifyKind(value, DateTimeKind.Utc);

        private static string? ValidateReason(string? reason)
        {
            return (reason?.Trim().Length ?? 0) > 500 ? "Reason cannot exceed 500 characters." : null;
        }

        private static string? ValidateRegistration(RegisterRequest request, out string code)
        {
            code = (request.CompanyCode ?? string.Empty).Trim().ToUpperInvariant();

            if (code.Length == 0)
            {
                return "Company code is required.";
            }

            if (!CompanyCodePattern.IsMatch(code))
            {
                return "Company code must be 2 to 50 characters: letters, numbers, hyphens and underscores only.";
            }

            var name = (request.CompanyName ?? string.Empty).Trim();

            if (name.Length == 0)
            {
                return "Company name is required.";
            }

            if (name.Length > 200)
            {
                return "Company name cannot exceed 200 characters.";
            }

            var address = (request.BusinessAddress ?? string.Empty).Trim();

            if (address.Length == 0)
            {
                return "Business address is required.";
            }

            if (address.Length > 300)
            {
                return "Business address cannot exceed 300 characters.";
            }

            var contact = (request.ContactNo ?? string.Empty).Trim();

            if (!PhonePattern.IsMatch(contact))
            {
                return "Contact number must be 7 to 20 characters (digits, +, -, spaces, parentheses).";
            }

            var email = (request.Email ?? string.Empty).Trim();

            if (email.Length > 150 || !EmailPattern.IsMatch(email))
            {
                return "A valid email address (up to 150 characters) is required.";
            }

            if (request.PlanId <= 0)
            {
                return "Please select a plan.";
            }

            return null;
        }
    }
}
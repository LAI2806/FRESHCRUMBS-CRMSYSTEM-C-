using System.Net;
using System.Security.Claims;
using System.Text.Json;
using freshcrumbs.CRM.api.Authorization;
using freshcrumbs.CRM.api.Services;
using freshcrumbs.CRM.api.Services.Sync;
using freshcrumbs.CRM.domain.entities;
using freshcrumbs.CRM.infrastructure.data;
using freshcrumbs.CRM.infrastructure.services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace freshcrumbs.CRM.api.Controllers
{
    // Tenant User Management: a company ADMIN manages the STAFF / MANAGER accounts of its OWN company.
    // TenantAccessFilter has already matched the caller to {companyId}; ManageUsers limits every action to ADMIN.
    // Accounts live in the cloud master database, so the desktop (Local mode) forwards changes to the cloud.
    [ApiController]
    [Route("api/tenant/{companyId:int}/users")]
    public class TenantUsersController : ControllerBase
    {
        private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

        private readonly ITenantDbContextFactory _tenantFactory;
        private readonly ISubscriptionService _subscriptions;
        private readonly SyncOptions _syncOptions;
        private readonly IServiceProvider _services;

        public TenantUsersController(
            ITenantDbContextFactory tenantFactory,
            ISubscriptionService subscriptions,
            SyncOptions syncOptions,
            IServiceProvider services)
        {
            _tenantFactory = tenantFactory;
            _subscriptions = subscriptions;
            _syncOptions = syncOptions;
            _services = services;
        }

        // Nullable so an empty field reaches Validate (and its message) instead of the automatic 400.
        public class EmployeeRequest
        {
            public string? FirstName { get; set; }
            public string? LastName { get; set; }
            public string? Email { get; set; }
            public string? ContactNumber { get; set; }
            public string? Role { get; set; }
        }

        public class StatusRequest
        {
            public string? Status { get; set; }
        }

        public class EmployeeDto
        {
            public string Id { get; set; } = string.Empty;
            public string UserName { get; set; } = string.Empty;
            public string Email { get; set; } = string.Empty;
            public string FirstName { get; set; } = string.Empty;
            public string LastName { get; set; } = string.Empty;
            public string FullName { get; set; } = string.Empty;
            public string ContactNumber { get; set; } = string.Empty;
            public string Role { get; set; } = string.Empty;
            public string Status { get; set; } = string.Empty;
            public int? BranchId { get; set; }
            public string? BranchName { get; set; }
            public bool IsSelf { get; set; }
            public bool CanEdit { get; set; }
        }

        public class EmployeeListDto
        {
            // False when the desktop is offline and the list comes from the last cloud snapshot (read-only).
            public bool Online { get; set; }
            public List<EmployeeDto> Users { get; set; } = new();
        }

        public class CreatedEmployeeDto
        {
            public EmployeeDto User { get; set; } = new();

            // Shown once to the ADMIN; the employee must replace it at the first sign-in.
            public string TemporaryPassword { get; set; } = string.Empty;
        }

        private sealed record CloudCall(JsonElement? Data, IActionResult? Error, bool Offline, string? Token);

        private sealed record EmployeeInput(string FirstName, string LastName, string Email, string ContactNumber, string Role);

        [HttpGet]
        [RequireTenantPermission(TenantPermissions.ManageUsers)]
        public async Task<IActionResult> GetAll(int companyId)
        {
            EmployeeListDto list;

            if (_syncOptions.IsLocal)
            {
                var call = await SendToCloudAsync(companyId, HttpMethod.Get, string.Empty, null);

                if (call.Data is JsonElement data)
                {
                    list = data.Deserialize<EmployeeListDto>(Json) ?? new EmployeeListDto();
                    list.Online = true;
                }
                else if (call.Offline)
                {
                    list = new EmployeeListDto { Online = false, Users = GetSnapshotUsers(companyId) };
                }
                else
                {
                    return call.Error!;
                }
            }
            else
            {
                list = new EmployeeListDto { Online = true, Users = await GetCloudUsersAsync(companyId) };
            }

            await AddBranchesAsync(companyId, list.Users);
            MarkCaller(list.Users);

            return Ok(list);
        }

        [HttpPost]
        [RequireTenantPermission(TenantPermissions.ManageUsers)]
        public async Task<IActionResult> Create(int companyId, EmployeeRequest request)
        {
            if (_syncOptions.IsLocal)
            {
                return await ForwardChangeAsync(companyId, HttpMethod.Post, string.Empty, request);
            }

            var input = Validate(request, out var invalid);

            if (input == null)
            {
                return invalid!;
            }

            var userManager = _services.GetRequiredService<UserManager<ApplicationUser>>();

            if (await IsEmailInUseAsync(userManager, input.Email, null))
            {
                return Conflict(new { message = "Another account already uses this email address." });
            }

            var seatError = await _subscriptions.CheckCanActivateUserAsync(companyId);

            if (seatError != null)
            {
                return Conflict(new { message = seatError });
            }

            var user = new ApplicationUser
            {
                UserName = input.Email,
                Email = input.Email,
                TenantId = companyId,
                FirstName = input.FirstName,
                LastName = input.LastName,
                ContactNumber = input.ContactNumber,
                Role = input.Role,
                Status = TenantAccounts.Active
            };

            // The account and its "must change password" flag are saved together, or not at all.
            var masterDb = _services.GetRequiredService<MasterCrmDbContext>();
            await using var transaction = await masterDb.Database.BeginTransactionAsync();

            var (temporaryPassword, createError) = await TenantAccounts.CreateWithTemporaryPasswordAsync(userManager, user);

            if (temporaryPassword == null)
            {
                return BadRequest(new { message = createError });
            }

            await transaction.CommitAsync();

            var dto = ToDto(user);
            MarkCaller(new List<EmployeeDto> { dto });

            return Ok(new CreatedEmployeeDto { User = dto, TemporaryPassword = temporaryPassword });
        }

        [HttpPut("{userId}")]
        [RequireTenantPermission(TenantPermissions.ManageUsers)]
        public async Task<IActionResult> Update(int companyId, string userId, EmployeeRequest request)
        {
            if (_syncOptions.IsLocal)
            {
                return await ForwardChangeAsync(companyId, HttpMethod.Put, "/" + Uri.EscapeDataString(userId), request);
            }

            var userManager = _services.GetRequiredService<UserManager<ApplicationUser>>();
            var (user, denied) = await FindEditableAsync(userManager, companyId, userId);

            if (user == null)
            {
                return denied!;
            }

            var input = Validate(request, out var invalid);

            if (input == null)
            {
                return invalid!;
            }

            if (await IsEmailInUseAsync(userManager, input.Email, user.Id))
            {
                return Conflict(new { message = "Another account already uses this email address." });
            }

            user.FirstName = input.FirstName;
            user.LastName = input.LastName;
            user.ContactNumber = input.ContactNumber;
            user.Role = input.Role;

            await TenantAccounts.SetEmailAsync(userManager, user, input.Email);

            var updated = await userManager.UpdateAsync(user);

            if (!updated.Succeeded)
            {
                return BadRequest(new { message = Describe(updated) });
            }

            var dto = ToDto(user);
            MarkCaller(new List<EmployeeDto> { dto });

            return Ok(dto);
        }

        [HttpPut("{userId}/status")]
        [RequireTenantPermission(TenantPermissions.ManageUsers)]
        public async Task<IActionResult> SetStatus(int companyId, string userId, StatusRequest request)
        {
            if (_syncOptions.IsLocal)
            {
                return await ForwardChangeAsync(companyId, HttpMethod.Put, "/" + Uri.EscapeDataString(userId) + "/status", request);
            }

            var status = NormalizeStatus(request.Status);

            if (status == null)
            {
                return BadRequest(new { message = "Status must be Active or Inactive." });
            }

            if (userId == CallerId())
            {
                return BadRequest(new { message = "You cannot deactivate your own account." });
            }

            var userManager = _services.GetRequiredService<UserManager<ApplicationUser>>();
            var (user, denied) = await FindEditableAsync(userManager, companyId, userId);

            if (user == null)
            {
                return denied!;
            }

            bool reactivating = status == TenantAccounts.Active
                && !string.Equals(user.Status, TenantAccounts.Active, StringComparison.OrdinalIgnoreCase);

            if (reactivating)
            {
                var seatError = await _subscriptions.CheckCanActivateUserAsync(companyId);

                if (seatError != null)
                {
                    return Conflict(new { message = seatError });
                }
            }

            user.Status = status;

            var updated = await userManager.UpdateAsync(user);

            if (!updated.Succeeded)
            {
                return BadRequest(new { message = Describe(updated) });
            }

            var dto = ToDto(user);
            MarkCaller(new List<EmployeeDto> { dto });

            return Ok(dto);
        }

        // Only STAFF / MANAGER accounts of this company; ADMIN accounts (including the caller) are read-only here.
        private async Task<(ApplicationUser? User, IActionResult? Denied)> FindEditableAsync(
            UserManager<ApplicationUser> userManager,
            int companyId,
            string userId)
        {
            var user = await userManager.FindByIdAsync(userId);

            if (user == null || user.TenantId != companyId)
            {
                return (null, NotFound(new { message = "This account does not belong to this company." }));
            }

            if (user.Id == CallerId() || TenantRoles.Normalize(user.Role) == TenantRoles.Admin)
            {
                return (null, StatusCode(StatusCodes.Status403Forbidden, new
                {
                    code = "AdminAccount",
                    message = "ADMIN accounts cannot be changed in User Management."
                }));
            }

            return (user, null);
        }

        private EmployeeInput? Validate(EmployeeRequest request, out IActionResult? error)
        {
            var firstName = (request.FirstName ?? string.Empty).Trim();
            var lastName = (request.LastName ?? string.Empty).Trim();
            var email = (request.Email ?? string.Empty).Trim();
            var contact = (request.ContactNumber ?? string.Empty).Trim();
            var role = TenantRoles.Normalize(request.Role);

            error = null;

            var profileError = TenantAccounts.ValidateProfile(firstName, lastName, email, contact);

            if (profileError != null)
            {
                error = BadRequest(new { message = profileError });
            }
            else if (role != TenantRoles.Staff && role != TenantRoles.Manager)
            {
                error = BadRequest(new { message = "Role must be STAFF or MANAGER." });
            }

            return error == null ? new EmployeeInput(firstName, lastName, email, contact, role!) : null;
        }

        private Task<bool> IsEmailInUseAsync(UserManager<ApplicationUser> userManager, string email, string? exceptUserId)
        {
            return TenantAccounts.IsEmailInUseAsync(
                userManager, _services.GetRequiredService<MasterCrmDbContext>(), email, exceptUserId);
        }

        private async Task<List<EmployeeDto>> GetCloudUsersAsync(int companyId)
        {
            var masterDb = _services.GetRequiredService<MasterCrmDbContext>();

            var users = await masterDb.Users.AsNoTracking()
                .Where(u => u.TenantId == companyId)
                .OrderBy(u => u.FirstName)
                .ThenBy(u => u.LastName)
                .ToListAsync();

            return users.Select(ToDto).ToList();
        }

        private List<EmployeeDto> GetSnapshotUsers(int companyId)
        {
            var cache = _services.GetRequiredService<LocalAccessCache>();

            var users = cache.Read(d => d.Companies.TryGetValue(companyId, out var c)
                ? c.Snapshot.Users.ToList()
                : new List<CompanyUserDto>());

            return users
                .Select(u => new EmployeeDto
                {
                    Id = u.Id,
                    UserName = u.UserName,
                    Email = u.Email,
                    FirstName = u.FirstName,
                    LastName = u.LastName,
                    FullName = u.FullName,
                    Role = TenantRoles.Normalize(u.Role) ?? u.Role,
                    Status = u.Status
                })
                .OrderBy(u => u.FullName)
                .ToList();
        }

        // Branch assignments live in the tenant database (Prompt 3) and are shown only on Branching plans.
        private async Task AddBranchesAsync(int companyId, List<EmployeeDto> users)
        {
            foreach (var user in users)
            {
                user.BranchId = null;
                user.BranchName = null;
            }

            if (!BranchStock.IsBranchingCompany(HttpContext) || users.Count == 0)
            {
                return;
            }

            await using var tenantDb = await _tenantFactory.CreateAsync(companyId);

            var assignments = await tenantDb.BranchAssignments.AsNoTracking()
                .Where(a => a.BranchId != null)
                .ToDictionaryAsync(a => a.UserId, a => a.BranchId!.Value);

            var branchNames = await tenantDb.Branches.AsNoTracking()
                .ToDictionaryAsync(b => b.BranchId, b => b.BranchName);

            foreach (var user in users)
            {
                if (assignments.TryGetValue(user.Id, out var branchId))
                {
                    user.BranchId = branchId;
                    user.BranchName = branchNames.TryGetValue(branchId, out var name) ? name : null;
                }
            }
        }

        private void MarkCaller(List<EmployeeDto> users)
        {
            var callerId = CallerId();

            foreach (var user in users)
            {
                user.IsSelf = user.Id == callerId;
                user.CanEdit = !user.IsSelf
                    && (user.Role == TenantRoles.Staff || user.Role == TenantRoles.Manager);
            }
        }

        private static EmployeeDto ToDto(ApplicationUser user)
        {
            return new EmployeeDto
            {
                Id = user.Id,
                UserName = user.UserName ?? string.Empty,
                Email = user.Email ?? string.Empty,
                FirstName = user.FirstName,
                LastName = user.LastName,
                FullName = $"{user.FirstName} {user.LastName}".Trim(),
                ContactNumber = user.ContactNumber,
                Role = TenantRoles.Normalize(user.Role) ?? user.Role,
                Status = user.Status
            };
        }

        private static string? NormalizeStatus(string? status)
        {
            var value = status?.Trim();

            if (string.Equals(value, TenantAccounts.Active, StringComparison.OrdinalIgnoreCase))
            {
                return TenantAccounts.Active;
            }

            return string.Equals(value, TenantAccounts.Inactive, StringComparison.OrdinalIgnoreCase)
                ? TenantAccounts.Inactive
                : null;
        }

        private static string Describe(IdentityResult result)
        {
            return string.Join(" ", result.Errors.Select(e => e.Description));
        }

        private string? CallerId()
        {
            return User.FindFirstValue(ClaimTypes.NameIdentifier);
        }

        // ---------------------------------------------------------------- Local mode (desktop)

        private async Task<IActionResult> ForwardChangeAsync(int companyId, HttpMethod method, string path, object body)
        {
            var call = await SendToCloudAsync(companyId, method, path, body);

            if (call.Data is not JsonElement data)
            {
                return call.Error!;
            }

            // The snapshot carries the company's accounts; refresh it so branch assignment and offline
            // sign-in see the change right away instead of at the next background sync.
            await RefreshSnapshotAsync(companyId, call.Token!);

            return Ok(data);
        }

        private async Task<CloudCall> SendToCloudAsync(int companyId, HttpMethod method, string path, object? body)
        {
            var sessions = _services.GetRequiredService<CloudSessionTokens>();
            var callerId = CallerId();
            var token = sessions.Get(callerId);

            if (token == null)
            {
                return new CloudCall(null, CloudRequired(
                    "Managing employees needs an online sign-in. Sign out, sign in again while connected to the internet, then try again."),
                    true, null);
            }

            var cloud = _services.GetRequiredService<CloudApiClient>();
            var result = await cloud.SendJsonAsync(method, $"api/tenant/{companyId}/users{path}", token, body, HttpContext.RequestAborted);

            if (result.Unreachable)
            {
                return new CloudCall(null, CloudRequired(
                    "The FreshCrumbs cloud service could not be reached. Managing employees needs the internet."),
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

        private async Task RefreshSnapshotAsync(int companyId, string token)
        {
            var cloud = _services.GetRequiredService<CloudApiClient>();
            var cache = _services.GetRequiredService<LocalAccessCache>();

            var snapshot = await cloud.GetSnapshotAsync(token, companyId, HttpContext.RequestAborted);

            if (!snapshot.Ok || snapshot.Data == null)
            {
                return;
            }

            cache.Update(doc =>
            {
                doc.Companies[companyId] = new CachedCompany
                {
                    Snapshot = snapshot.Data,
                    LastCloudValidatedUtc = DateTime.UtcNow
                };
            });
        }

        // 503 (not 401) so the desktop never mistakes a missing cloud session for its own expired session.
        private ObjectResult CloudRequired(string message)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { code = "CloudRequired", message });
        }
    }
}

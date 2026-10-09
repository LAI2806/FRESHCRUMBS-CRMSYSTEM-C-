using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using freshcrumbs.CRM.domain.entities;
using freshcrumbs.CRM.infrastructure.data;
using freshcrumbs.CRM.api.Services;

namespace freshcrumbs.CRM.api.Controllers
{
    [Authorize(Roles = PlatformRoles.SuperAdmin)]
    [ApiController]
    [Route("api/[controller]")]
    public class UsersController : ControllerBase
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly MasterCrmDbContext _masterDb;

        private readonly ISubscriptionService _subscriptions;

        public UsersController(
            UserManager<ApplicationUser> userManager,
            MasterCrmDbContext masterDb,
            ISubscriptionService subscriptions)
        {
            _userManager = userManager;
            _masterDb = masterDb;
            _subscriptions = subscriptions;
        }

        public class CreateUserRequest
        {
            public int TenantId { get; set; }
            public string FirstName { get; set; } = string.Empty;
            public string LastName { get; set; } = string.Empty;
            public string Username { get; set; } = string.Empty;
            public string Password { get; set; } = string.Empty;
            public string ContactNumber { get; set; } = string.Empty;
            public string Email { get; set; } = string.Empty;
            public string Role { get; set; } = string.Empty;
        }

        public class UpdateUserRequest
        {
            public string FirstName { get; set; } = string.Empty;
            public string LastName { get; set; } = string.Empty;
            public string ContactNumber { get; set; } = string.Empty;
            public string Email { get; set; } = string.Empty;
            public string Role { get; set; } = string.Empty;
            public string Status { get; set; } = string.Empty;
        }

        public class ResetPasswordRequest
        {
            public string NewPassword { get; set; } = string.Empty;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var users = await _masterDb.Users
                .Select(x => new
                {
                    x.Id,
                    x.UserName,
                    x.Email,
                    x.TenantId,
                    x.FirstName,
                    x.LastName,
                    x.ContactNumber,
                    x.Role,
                    x.Status
                })
                .ToListAsync();

            return Ok(users);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(string id)
        {
            var user = await _userManager.FindByIdAsync(id);

            if (user == null)
            {
                return NotFound($"User with id {id} not found.");
            }

            return Ok(new
            {
                user.Id,
                user.UserName,
                user.Email,
                user.TenantId,
                user.FirstName,
                user.LastName,
                user.ContactNumber,
                user.Role,
                user.Status
            });
        }

        [HttpGet("tenant/{tenantId:int}")]
        public async Task<IActionResult> GetUsersByTenant(int tenantId)
        {
            var users = await _masterDb.Users
                .Where(x => x.TenantId == tenantId)
                .Select(x => new
                {
                    x.Id,
                    x.UserName,
                    x.Email,
                    x.FirstName,
                    x.LastName,
                    x.Role,
                    x.Status
                })
                .ToListAsync();

            return Ok(users);
        }

        [HttpPost]
        public async Task<IActionResult> CreateUser(CreateUserRequest request)
        {
            var companyExists = await _masterDb.Companies
                .AnyAsync(x => x.CompanyId == request.TenantId);

            if (!companyExists)
            {
                return BadRequest($"TenantId {request.TenantId} does not match any existing company.");
            }

            var role = TenantRoles.Normalize(request.Role);

            if (role == null)
            {
                return BadRequest(new { message = $"Role must be one of: {string.Join(", ", TenantRoles.All)}." });
            }

            var seatError = await _subscriptions.CheckCanActivateUserAsync(request.TenantId);

            if (seatError != null)
            {
                return Conflict(new { message = seatError });
            }
            var user = new ApplicationUser
            {
                UserName = request.Username,
                Email = request.Email,
                TenantId = request.TenantId,
                FirstName = request.FirstName,
                LastName = request.LastName,
                ContactNumber = request.ContactNumber,
                Role = role,
                Status = "Active"
            };

            var result = await _userManager.CreateAsync(user, request.Password);

            if (!result.Succeeded)
            {
                return BadRequest(result.Errors);
            }

            return Created($"api/users/{user.Id}", new { user.Id, user.UserName, user.Email });
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateUser(string id, UpdateUserRequest request)
        {
            var user = await _userManager.FindByIdAsync(id);

            if (user == null)
            {
                return NotFound($"User with id {id} not found.");
            }

            // Tenant users are restricted to ADMIN / MANAGER / STAFF. Platform accounts (no tenant) keep their role untouched.
            var role = user.Role;

            if (user.TenantId != null)
            {
                var normalizedRole = TenantRoles.Normalize(request.Role);

                if (normalizedRole == null)
                {
                    return BadRequest(new { message = $"Role must be one of: {string.Join(", ", TenantRoles.All)}." });
                }

                role = normalizedRole;
            }

            var isReactivating = user.TenantId != null
                && !string.Equals(user.Status, "Active", StringComparison.OrdinalIgnoreCase)
                && string.Equals(request.Status, "Active", StringComparison.OrdinalIgnoreCase);

            if (isReactivating)
            {
                var seatError = await _subscriptions.CheckCanActivateUserAsync(user.TenantId!.Value);

                if (seatError != null)
                {
                    return Conflict(new { message = seatError });
                }
            }

            user.FirstName = request.FirstName;
            user.LastName = request.LastName;
            user.ContactNumber = request.ContactNumber;
            user.Email = request.Email;
            user.Role = role;
            user.Status = request.Status;

            var result = await _userManager.UpdateAsync(user);

            if (!result.Succeeded)
            {
                return BadRequest(result.Errors);
            }

            return Ok(new
            {
                user.Id,
                user.UserName,
                user.Email,
                user.TenantId,
                user.FirstName,
                user.LastName,
                user.ContactNumber,
                user.Role,
                user.Status
            });
        }

        // SuperAdmin-only: sets a new password for an existing user without needing the current one.
        // Uses the standard Identity token flow so the configured password rules are enforced.
        [HttpPost("{id}/reset-password")]
        [Authorize(Roles = PlatformRoles.SuperAdmin)]
        public async Task<IActionResult> ResetPassword(string id, ResetPasswordRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.NewPassword))
            {
                return BadRequest(new { message = "NewPassword is required." });
            }

            var user = await _userManager.FindByIdAsync(id);

            if (user == null)
            {
                return NotFound($"User with id {id} not found.");
            }

            var token = await _userManager.GeneratePasswordResetTokenAsync(user);
            var result = await _userManager.ResetPasswordAsync(user, token, request.NewPassword);

            if (!result.Succeeded)
            {
                return BadRequest(result.Errors);
            }

            // Clear any lockout caused by earlier failed login attempts.
            await _userManager.SetLockoutEndDateAsync(user, null);
            await _userManager.ResetAccessFailedCountAsync(user);

            return Ok(new { message = "Password reset successfully.", user.Id, user.UserName });
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteUser(string id)
        {
            var user = await _userManager.FindByIdAsync(id);

            if (user == null)
            {
                return NotFound($"User with id {id} not found.");
            }

            var result = await _userManager.DeleteAsync(user);

            if (!result.Succeeded)
            {
                return BadRequest(result.Errors);
            }

            return NoContent();
        }
    }
}
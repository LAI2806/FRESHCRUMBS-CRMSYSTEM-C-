using System.Security.Claims;
using freshcrumbs.CRM.api.Authorization;
using freshcrumbs.CRM.api.Services;
using freshcrumbs.CRM.api.Services.Sync;
using freshcrumbs.CRM.domain.entities;
using freshcrumbs.CRM.infrastructure.data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using System.Text;

namespace freshcrumbs.CRM.api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly MasterCrmDbContext _masterDb;
        private readonly IConfiguration _configuration;

        public AuthController(
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            MasterCrmDbContext masterDb,
            IConfiguration configuration)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _masterDb = masterDb;
            _configuration = configuration;
        }

        public class LoginRequest
        {
            public string UserName { get; set; } = string.Empty;
            public string Password { get; set; } = string.Empty;
        }

        public class ChangePasswordRequest
        {
            public string? CurrentPassword { get; set; }
            public string? NewPassword { get; set; }
        }

        public class AccountRequest
        {
            public string? FirstName { get; set; }
            public string? LastName { get; set; }
            public string? Email { get; set; }
            public string? ContactNumber { get; set; }
        }

        [HttpPost("login")]
        [AllowAnonymous]
        public async Task<IActionResult> Login(
            LoginRequest request,
            [FromServices] SyncOptions syncOptions,
            [FromServices] IServiceProvider services)
        {
            if (string.IsNullOrWhiteSpace(request.UserName) || string.IsNullOrWhiteSpace(request.Password))
            {
                return BadRequest(new { message = "Username and password are required." });
            }

            // Desktop (Local mode): cloud login when online, cached hash within the grace period when offline.
            if (syncOptions.IsLocal)
            {
                var localLogin = services.GetRequiredService<LocalLoginService>();
                return await localLogin.LoginAsync(request.UserName, request.Password, HttpContext.RequestAborted);
            }

            var user = await _userManager.FindByNameAsync(request.UserName.Trim())
                ?? await _userManager.FindByEmailAsync(request.UserName.Trim());

            if (user == null)
            {
                return Unauthorized(new { message = "Invalid username or password." });
            }

            var signIn = await _signInManager.CheckPasswordSignInAsync(user, request.Password, lockoutOnFailure: true);

            if (signIn.IsLockedOut)
            {
                return StatusCode(StatusCodes.Status403Forbidden,
                    new { message = "This account is temporarily locked. Please try again later." });
            }

            if (!signIn.Succeeded)
            {
                return Unauthorized(new { message = "Invalid username or password." });
            }

            if (!string.Equals(user.Status, "Active", StringComparison.OrdinalIgnoreCase))
            {
                return StatusCode(StatusCodes.Status403Forbidden,
                    new { message = "This account is inactive. Please contact your administrator." });
            }

            var roles = await _userManager.GetRolesAsync(user);
            var isSuperAdmin = roles.Contains(PlatformRoles.SuperAdmin);

            Company? company = null;

            if (!isSuperAdmin)
            {
                if (user.TenantId == null)
                {
                    return StatusCode(StatusCodes.Status403Forbidden,
                        new { message = "This account is not assigned to a company." });
                }

                company = await _masterDb.Companies
                    .AsNoTracking()
                    .FirstOrDefaultAsync(x => x.CompanyId == user.TenantId.Value);

                if (company == null || !company.IsActive)
                {
                    return StatusCode(StatusCodes.Status403Forbidden,
                        new { message = "The company for this account is not active." });
                }

                if (!TenantRoles.IsValid(user.Role))
                {
                    return StatusCode(StatusCodes.Status403Forbidden,
                        new { message = "This account does not have a valid role (ADMIN, MANAGER or STAFF). Please contact your administrator." });
                }
            }

            var expiresAt = DateTime.UtcNow.AddMinutes(_configuration.GetValue("Jwt:ExpiryMinutes", 480));
            var mustChangePassword = !isSuperAdmin && await MustChangePasswordAsync(user);
            var token = CreateToken(user, roles, isSuperAdmin ? null : user.TenantId, expiresAt, null, mustChangePassword);

            return Ok(BuildSession(user, roles, company, token, expiresAt, mustChangePassword));
        }

        // Cloud only. Issues a longer-lived token that can ONLY be used for api/tenant/{id}/sync/*
        // (TenantAccessFilter enforces that). The desktop uses it to synchronize in the background,
        // e.g. when the internet returns while the user signed in offline.
        [HttpPost("sync-token")]
        [Authorize]
        public async Task<IActionResult> SyncToken([FromServices] SyncOptions syncOptions)
        {
            if (User.HasClaim(c => c.Type == TenantAccessFilter.PurposeClaim))
            {
                return StatusCode(StatusCodes.Status403Forbidden,
                    new { message = "A sync token cannot be used to create another token." });
            }

            if (User.HasClaim(c => c.Type == TenantAccessFilter.PasswordChangeClaim))
            {
                return StatusCode(StatusCodes.Status403Forbidden,
                    new { message = "Set your own password before this computer can synchronize." });
            }

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var user = userId == null ? null : await _userManager.FindByIdAsync(userId);

            if (user == null
                || user.TenantId == null
                || !string.Equals(user.Status, "Active", StringComparison.OrdinalIgnoreCase))
            {
                return Unauthorized();
            }

            var roles = await _userManager.GetRolesAsync(user);
            var expiresAt = DateTime.UtcNow.AddDays(Math.Max(1, syncOptions.OfflineGraceDays));
            var token = CreateToken(user, roles, user.TenantId, expiresAt, TenantAccessFilter.SyncPurpose);

            return Ok(new { token, expiresAt });
        }

        [HttpGet("me")]
        [Authorize]
        public async Task<IActionResult> Me()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var user = userId == null ? null : await _userManager.FindByIdAsync(userId);

            if (user == null || !string.Equals(user.Status, "Active", StringComparison.OrdinalIgnoreCase))
            {
                return Unauthorized();
            }

            var roles = await _userManager.GetRolesAsync(user);
            Company? company = null;

            if (user.TenantId != null)
            {
                company = await _masterDb.Companies
                    .AsNoTracking()
                    .FirstOrDefaultAsync(x => x.CompanyId == user.TenantId.Value);
            }

            return Ok(BuildSession(user, roles, company, null, null, await MustChangePasswordAsync(user)));
        }

        // Tenant employees change their own password (required after signing in with a temporary password).
        // Desktop (Local mode): forwarded to the cloud, where the account lives.
        [HttpPost("change-password")]
        [Authorize]
        public async Task<IActionResult> ChangePassword(
            ChangePasswordRequest request,
            [FromServices] SyncOptions syncOptions,
            [FromServices] IServiceProvider services)
        {
            if (User.HasClaim(c => c.Type == TenantAccessFilter.PurposeClaim))
            {
                return StatusCode(StatusCodes.Status403Forbidden,
                    new { message = "A sync token cannot be used to change a password." });
            }

            if (string.IsNullOrEmpty(request.CurrentPassword) || string.IsNullOrEmpty(request.NewPassword))
            {
                return BadRequest(new { message = "Current password and new password are required." });
            }

            if (request.CurrentPassword == request.NewPassword)
            {
                return BadRequest(new { message = "The new password must be different from the current password." });
            }

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (syncOptions.IsLocal)
            {
                var localLogin = services.GetRequiredService<LocalLoginService>();
                return await localLogin.ChangePasswordAsync(userId, request.CurrentPassword, request.NewPassword, HttpContext.RequestAborted);
            }

            var user = userId == null ? null : await _userManager.FindByIdAsync(userId);

            if (user == null
                || user.TenantId == null
                || !string.Equals(user.Status, "Active", StringComparison.OrdinalIgnoreCase))
            {
                return Unauthorized(new { message = "Please log in to continue." });
            }

            var result = await _userManager.ChangePasswordAsync(user, request.CurrentPassword, request.NewPassword);

            if (!result.Succeeded)
            {
                return BadRequest(new { message = string.Join(" ", result.Errors.Select(e => e.Description)) });
            }

            var temporary = (await _userManager.GetClaimsAsync(user))
                .Where(c => c.Type == TenantAccounts.MustChangePasswordClaim)
                .ToList();

            if (temporary.Count > 0)
            {
                await _userManager.RemoveClaimsAsync(user, temporary);
            }

            return Ok(new { message = "Password changed." });
        }

        // My Account (MainCRM): always the signed-in tenant user's OWN account, taken from the token; no user id is accepted.
        // Desktop (Local mode): read from the cloud when online, otherwise a read-only copy from this computer.
        [HttpGet("me/account")]
        [Authorize]
        public async Task<IActionResult> GetMyAccount(
            [FromServices] SyncOptions syncOptions,
            [FromServices] IServiceProvider services)
        {
            if (IsLimitedToken())
            {
                return StatusCode(StatusCodes.Status403Forbidden, new { message = "This session cannot open My Account." });
            }

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (syncOptions.IsLocal)
            {
                var localLogin = services.GetRequiredService<LocalLoginService>();
                return await localLogin.GetAccountAsync(userId, HttpContext.RequestAborted);
            }

            var user = await FindActiveTenantUserAsync(userId);

            if (user == null)
            {
                return Unauthorized(new { message = "Please log in to continue." });
            }

            return Ok(await ToAccountAsync(user));
        }

        // Editable: name, email (the sign-in name) and contact number. Role and company never change here.
        [HttpPut("me/account")]
        [Authorize]
        public async Task<IActionResult> UpdateMyAccount(
            AccountRequest request,
            [FromServices] SyncOptions syncOptions,
            [FromServices] IServiceProvider services)
        {
            if (IsLimitedToken())
            {
                return StatusCode(StatusCodes.Status403Forbidden, new { message = "This session cannot change My Account." });
            }

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (syncOptions.IsLocal)
            {
                var localLogin = services.GetRequiredService<LocalLoginService>();
                return await localLogin.UpdateAccountAsync(userId, request, HttpContext.RequestAborted);
            }

            var user = await FindActiveTenantUserAsync(userId);

            if (user == null)
            {
                return Unauthorized(new { message = "Please log in to continue." });
            }

            var firstName = (request.FirstName ?? string.Empty).Trim();
            var lastName = (request.LastName ?? string.Empty).Trim();
            var email = (request.Email ?? string.Empty).Trim();
            var contact = (request.ContactNumber ?? string.Empty).Trim();

            var profileError = TenantAccounts.ValidateProfile(firstName, lastName, email, contact);

            if (profileError != null)
            {
                return BadRequest(new { message = profileError });
            }

            if (await TenantAccounts.IsEmailInUseAsync(_userManager, _masterDb, email, user.Id))
            {
                return Conflict(new { message = "Another account already uses this email address." });
            }

            user.FirstName = firstName;
            user.LastName = lastName;
            user.ContactNumber = contact;
            await TenantAccounts.SetEmailAsync(_userManager, user, email);

            var result = await _userManager.UpdateAsync(user);

            if (!result.Succeeded)
            {
                return BadRequest(new { message = string.Join(" ", result.Errors.Select(e => e.Description)) });
            }

            return Ok(await ToAccountAsync(user));
        }

        // Tokens are self-contained, so there is nothing to revoke in the cloud. The desktop (Local mode) forgets the
        // in-memory cloud session it kept for this user.
        [HttpPost("logout")]
        [Authorize]
        public IActionResult Logout([FromServices] SyncOptions syncOptions, [FromServices] IServiceProvider services)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (syncOptions.IsLocal && userId != null)
            {
                services.GetRequiredService<CloudSessionTokens>().Remove(userId);
            }

            return Ok(new { message = "Logged out." });
        }

        private bool IsLimitedToken()
        {
            return User.HasClaim(c => c.Type == TenantAccessFilter.PurposeClaim)
                || User.HasClaim(c => c.Type == TenantAccessFilter.PasswordChangeClaim);
        }

        private async Task<ApplicationUser?> FindActiveTenantUserAsync(string? userId)
        {
            var user = userId == null ? null : await _userManager.FindByIdAsync(userId);

            return user != null
                && user.TenantId != null
                && string.Equals(user.Status, "Active", StringComparison.OrdinalIgnoreCase)
                ? user
                : null;
        }

        private async Task<object> ToAccountAsync(ApplicationUser user)
        {
            var companyName = await _masterDb.Companies
                .AsNoTracking()
                .Where(c => c.CompanyId == user.TenantId)
                .Select(c => c.CompanyName)
                .FirstOrDefaultAsync();

            return new
            {
                user.FirstName,
                user.LastName,
                FullName = $"{user.FirstName} {user.LastName}".Trim(),
                user.UserName,
                user.Email,
                user.ContactNumber,
                Role = TenantRoles.Normalize(user.Role),
                CompanyName = companyName ?? string.Empty,
                Online = true
            };
        }

        private async Task<bool> MustChangePasswordAsync(ApplicationUser user)
        {
            return (await _userManager.GetClaimsAsync(user)).Any(c => c.Type == TenantAccounts.MustChangePasswordClaim);
        }

        private static object BuildSession(
            ApplicationUser user,
            IList<string> roles,
            Company? company,
            string? token,
            DateTime? expiresAt,
            bool mustChangePassword)
        {
            return new
            {
                token,
                expiresAt,
                userId = user.Id,
                userName = user.UserName,
                fullName = $"{user.FirstName} {user.LastName}".Trim(),
                email = user.Email,
                roles,
                isSuperAdmin = roles.Contains(PlatformRoles.SuperAdmin),
                // Tenant role (ADMIN / MANAGER / STAFF); null for SuperAdmin, which is a platform role.
                role = roles.Contains(PlatformRoles.SuperAdmin) ? null : TenantRoles.Normalize(user.Role),
                tenantId = company?.CompanyId,
                companyCode = company?.CompanyCode,
                companyName = company?.CompanyName,
                mustChangePassword
            };
        }

        private string CreateToken(
            ApplicationUser user,
            IList<string> roles,
            int? tenantId,
            DateTime expiresAt,
            string? purpose = null,
            bool mustChangePassword = false)
        {
            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, user.Id),
                new(ClaimTypes.Name, user.UserName ?? string.Empty)
            };

            claims.AddRange(roles.Select(role => new Claim(ClaimTypes.Role, role)));

            if (tenantId != null)
            {
                claims.Add(new Claim(TenantAccessRequirement.TenantIdClaim, tenantId.Value.ToString()));
            }

            if (purpose != null)
            {
                claims.Add(new Claim(TenantAccessFilter.PurposeClaim, purpose));
            }

            if (mustChangePassword)
            {
                claims.Add(new Claim(TenantAccessFilter.PasswordChangeClaim, "true"));
            }

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_configuration["Jwt:Key"]!));

            var descriptor = new SecurityTokenDescriptor
            {
                Issuer = _configuration["Jwt:Issuer"],
                Audience = _configuration["Jwt:Audience"],
                Subject = new ClaimsIdentity(claims),
                Expires = expiresAt,
                SigningCredentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256)
            };

            return new JsonWebTokenHandler().CreateToken(descriptor);
        }
    }
}
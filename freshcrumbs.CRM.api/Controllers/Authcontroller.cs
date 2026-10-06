using System.Security.Claims;
using freshcrumbs.CRM.api.Authorization;
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

        [HttpPost("login")]
        [AllowAnonymous]
        public async Task<IActionResult> Login(LoginRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.UserName) || string.IsNullOrWhiteSpace(request.Password))
            {
                return BadRequest(new { message = "Username and password are required." });
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
            }

            var expiresAt = DateTime.UtcNow.AddMinutes(_configuration.GetValue("Jwt:ExpiryMinutes", 480));
            var token = CreateToken(user, roles, isSuperAdmin ? null : user.TenantId, expiresAt);

            return Ok(BuildSession(user, roles, company, token, expiresAt));
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

            return Ok(BuildSession(user, roles, company, null, null));
        }

        private static object BuildSession(
            ApplicationUser user,
            IList<string> roles,
            Company? company,
            string? token,
            DateTime? expiresAt)
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
                tenantId = company?.CompanyId,
                companyCode = company?.CompanyCode,
                companyName = company?.CompanyName
            };
        }

        private string CreateToken(ApplicationUser user, IList<string> roles, int? tenantId, DateTime expiresAt)
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
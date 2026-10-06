using System.Security.Claims;
using System.Text.RegularExpressions;
using freshcrumbs.CRM.domain.entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace freshcrumbs.CRM.api.Controllers
{
    [ApiController]
    [Authorize(Roles = PlatformRoles.SuperAdmin)]
    [Route("api/platform/account")]
    public class AccountController : ControllerBase
    {
        private static readonly Regex EmailPattern = new(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.Compiled);
        private static readonly Regex PhonePattern = new(@"^[0-9+\-\s()]{7,20}$", RegexOptions.Compiled);

        private readonly UserManager<ApplicationUser> _userManager;

        public AccountController(UserManager<ApplicationUser> userManager)
        {
            _userManager = userManager;
        }

        public class UpdateAccountRequest
        {
            public string FirstName { get; set; } = string.Empty;
            public string LastName { get; set; } = string.Empty;
            public string Email { get; set; } = string.Empty;
            public string ContactNumber { get; set; } = string.Empty;
        }

        public class ChangePasswordRequest
        {
            public string CurrentPassword { get; set; } = string.Empty;
            public string NewPassword { get; set; } = string.Empty;
        }

        [HttpGet]
        public async Task<IActionResult> Get()
        {
            var user = await GetCurrentUserAsync();

            return user == null ? Unauthorized() : Ok(ToResponse(user));
        }

        [HttpPut]
        public async Task<IActionResult> Update(UpdateAccountRequest request)
        {
            var user = await GetCurrentUserAsync();

            if (user == null)
            {
                return Unauthorized();
            }

            var firstName = (request.FirstName ?? string.Empty).Trim();
            var lastName = (request.LastName ?? string.Empty).Trim();
            var email = (request.Email ?? string.Empty).Trim();
            var contact = (request.ContactNumber ?? string.Empty).Trim();

            if (firstName.Length == 0 || firstName.Length > 100)
            {
                return BadRequest(new { message = "First name is required (up to 100 characters)." });
            }

            if (lastName.Length == 0 || lastName.Length > 100)
            {
                return BadRequest(new { message = "Last name is required (up to 100 characters)." });
            }

            if (email.Length > 256 || !EmailPattern.IsMatch(email))
            {
                return BadRequest(new { message = "A valid email address is required." });
            }

            if (contact.Length > 0 && !PhonePattern.IsMatch(contact))
            {
                return BadRequest(new { message = "Contact number must be 7 to 20 characters (digits, +, -, spaces, parentheses)." });
            }

            var existing = await _userManager.FindByEmailAsync(email);

            if (existing != null && existing.Id != user.Id)
            {
                return Conflict(new { message = "Another account already uses this email address." });
            }

            user.FirstName = firstName;
            user.LastName = lastName;
            user.ContactNumber = contact;
            user.Email = email;

            await _userManager.UpdateNormalizedEmailAsync(user);

            var result = await _userManager.UpdateAsync(user);

            if (!result.Succeeded)
            {
                return BadRequest(new { message = string.Join(" ", result.Errors.Select(e => e.Description)) });
            }

            return Ok(ToResponse(user));
        }

        [HttpPost("change-password")]
        public async Task<IActionResult> ChangePassword(ChangePasswordRequest request)
        {
            var user = await GetCurrentUserAsync();

            if (user == null)
            {
                return Unauthorized();
            }

            if (string.IsNullOrEmpty(request.CurrentPassword) || string.IsNullOrEmpty(request.NewPassword))
            {
                return BadRequest(new { message = "Current password and new password are required." });
            }

            var result = await _userManager.ChangePasswordAsync(user, request.CurrentPassword, request.NewPassword);

            if (!result.Succeeded)
            {
                return BadRequest(new { message = string.Join(" ", result.Errors.Select(e => e.Description)) });
            }

            return Ok(new { message = "Password changed." });
        }

        private async Task<ApplicationUser?> GetCurrentUserAsync()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            return userId == null ? null : await _userManager.FindByIdAsync(userId);
        }

        private static object ToResponse(ApplicationUser user)
        {
            return new
            {
                user.FirstName,
                user.LastName,
                FullName = $"{user.FirstName} {user.LastName}".Trim(),
                user.UserName,
                user.Email,
                user.ContactNumber
            };
        }
    }
}
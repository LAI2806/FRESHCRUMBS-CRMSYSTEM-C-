using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using freshcrumbs.CRM.domain.entities;
using freshcrumbs.CRM.infrastructure.data;

namespace freshcrumbs.CRM.api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class UsersController : ControllerBase
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly MasterCrmDbContext _masterDb;

        public UsersController(
            UserManager<ApplicationUser> userManager,
            MasterCrmDbContext masterDb)
        {
            _userManager = userManager;
            _masterDb = masterDb;
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

            var user = new ApplicationUser
            {
                UserName = request.Username,
                Email = request.Email,
                TenantId = request.TenantId,
                FirstName = request.FirstName,
                LastName = request.LastName,
                ContactNumber = request.ContactNumber,
                Role = request.Role,
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

            user.FirstName = request.FirstName;
            user.LastName = request.LastName;
            user.ContactNumber = request.ContactNumber;
            user.Email = request.Email;
            user.Role = request.Role;
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
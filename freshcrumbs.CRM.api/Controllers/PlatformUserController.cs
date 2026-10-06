using freshcrumbs.CRM.domain.entities;
using freshcrumbs.CRM.infrastructure.data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace freshcrumbs.CRM.api.Controllers
{
    [ApiController]
    [Authorize(Roles = PlatformRoles.SuperAdmin)]
    [Route("api/platform/users")]
    public class PlatformUsersController : ControllerBase
    {
        private readonly MasterCrmDbContext _db;
        private readonly UserManager<ApplicationUser> _userManager;

        public PlatformUsersController(MasterCrmDbContext db, UserManager<ApplicationUser> userManager)
        {
            _db = db;
            _userManager = userManager;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var superAdminIds = (await _userManager.GetUsersInRoleAsync(PlatformRoles.SuperAdmin))
                .Select(u => u.Id)
                .ToHashSet();

            var companies = await _db.Companies.AsNoTracking()
                .Select(c => new { c.CompanyId, c.CompanyCode, c.CompanyName })
                .ToDictionaryAsync(c => c.CompanyId);

            var users = await _db.Users.AsNoTracking()
                .Select(u => new { u.Id, u.FirstName, u.LastName, u.UserName, u.Email, u.Role, u.Status, u.TenantId })
                .ToListAsync();

            var rows = users
                .Select(u =>
                {
                    var isSuperAdmin = superAdminIds.Contains(u.Id);
                    var company = u.TenantId != null && companies.TryGetValue(u.TenantId.Value, out var found) ? found : null;

                    return new
                    {
                        UserId = u.Id,
                        FullName = $"{u.FirstName} {u.LastName}".Trim(),
                        u.UserName,
                        u.Email,
                        TenantId = u.TenantId,
                        TenantCode = company?.CompanyCode,
                        TenantName = isSuperAdmin ? "Platform" : company?.CompanyName ?? "-",
                        Role = isSuperAdmin ? "Super Admin" : (string.IsNullOrWhiteSpace(u.Role) ? "-" : u.Role),
                        u.Status
                    };
                })
                .OrderBy(u => u.TenantName)
                .ThenBy(u => u.FullName)
                .ToList();

            return Ok(rows);
        }
    }
}
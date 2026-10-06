using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using freshcrumbs.CRM.domain.entities;
using freshcrumbs.CRM.infrastructure.data;

namespace freshcrumbs.CRM.api.Controllers
{
    [Authorize(Roles = PlatformRoles.SuperAdmin)]
    [ApiController]
    [Route("api/[controller]")]
    public class CompanyDatabasesController : ControllerBase
    {
        private readonly MasterCrmDbContext _db;

        public CompanyDatabasesController(MasterCrmDbContext db)
        {
            _db = db;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var companyDatabases = await _db.CompanyDatabases
                .AsNoTracking()
                .OrderBy(x => x.CompanyDatabaseId)
                .ToListAsync();

            return Ok(companyDatabases);
        }

        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetById(int id)
        {
            var companyDatabase = await _db.CompanyDatabases
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.CompanyDatabaseId == id);

            if (companyDatabase == null)
            {
                return NotFound($"CompanyDatabase with id {id} not found.");
            }

            return Ok(companyDatabase);
        }

        [HttpPost]
        public async Task<IActionResult> CreateCompanyDatabase(CompanyDatabase companyDatabase)
        {
            var companyExists = await _db.Companies
                .AnyAsync(x => x.CompanyId == companyDatabase.CompanyId);

            if (!companyExists)
            {
                return BadRequest($"CompanyId {companyDatabase.CompanyId} does not exist.");
            }

            _db.CompanyDatabases.Add(companyDatabase);
            await _db.SaveChangesAsync();

            return Created(
                $"api/companydatabases/{companyDatabase.CompanyDatabaseId}",
                companyDatabase);
        }

        [HttpPut("{id:int}")]
        public async Task<IActionResult> UpdateCompanyDatabase(int id, CompanyDatabase updated)
        {
            var companyDatabase = await _db.CompanyDatabases
                .FirstOrDefaultAsync(x => x.CompanyDatabaseId == id);

            if (companyDatabase == null)
            {
                return NotFound($"CompanyDatabase with id {id} not found.");
            }

            companyDatabase.ServerName = updated.ServerName;
            companyDatabase.DatabaseName = updated.DatabaseName;
            companyDatabase.CredentialKey = updated.CredentialKey;
            companyDatabase.IsActive = updated.IsActive;

            await _db.SaveChangesAsync();

            return Ok(companyDatabase);
        }

        [HttpDelete("{id:int}")]
        public async Task<IActionResult> DeleteCompanyDatabase(int id)
        {
            var companyDatabase = await _db.CompanyDatabases
                .FirstOrDefaultAsync(x => x.CompanyDatabaseId == id);

            if (companyDatabase == null)
            {
                return NotFound($"CompanyDatabase with id {id} not found.");
            }

            _db.CompanyDatabases.Remove(companyDatabase);
            await _db.SaveChangesAsync();

            return NoContent();
        }
    }
}
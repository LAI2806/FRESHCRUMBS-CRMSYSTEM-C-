using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using freshcrumbs.CRM.domain.entities;
using freshcrumbs.CRM.infrastructure.data;

namespace freshcrumbs.CRM.api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class CompaniesController : ControllerBase
    {
        private readonly MasterCrmDbContext _db;

        public CompaniesController(MasterCrmDbContext db)
        {
            _db = db;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var companies = await _db.Companies
                .AsNoTracking()
                .OrderBy(x => x.CompanyId)
                .ToListAsync();

            return Ok(companies);
        }

        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetById(int id)
        {
            var company = await _db.Companies
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.CompanyId == id);

            if (company == null)
            {
                return NotFound($"Company with id {id} not found.");
            }

            return Ok(company);
        }

        [HttpPost]
        public async Task<IActionResult> CreateCompany(Company company)
        {
            _db.Companies.Add(company);
            await _db.SaveChangesAsync();

            return Created($"api/companies/{company.CompanyId}", company);
        }

        [HttpPut("{id:int}")]
        public async Task<IActionResult> UpdateCompany(int id, Company updatedCompany)
        {
            var company = await _db.Companies.FirstOrDefaultAsync(x => x.CompanyId == id);

            if (company == null)
            {
                return NotFound($"Company with id {id} not found.");
            }

            company.CompanyCode = updatedCompany.CompanyCode;
            company.CompanyName = updatedCompany.CompanyName;
            company.BusinessAddress = updatedCompany.BusinessAddress;
            company.ContactNo = updatedCompany.ContactNo;
            company.Email = updatedCompany.Email;
            company.Status = updatedCompany.Status;
            company.IsActive = updatedCompany.IsActive;

            await _db.SaveChangesAsync();

            return Ok(company);
        }

        [HttpDelete("{id:int}")]
        public async Task<IActionResult> DeleteCompany(int id)
        {
            var company = await _db.Companies.FirstOrDefaultAsync(x => x.CompanyId == id);

            if (company == null)
            {
                return NotFound($"Company with id {id} not found.");
            }

            _db.Companies.Remove(company);

            try
            {
                await _db.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                return Conflict($"Cannot delete Company {id} because it has existing related records (Users or CompanyDatabases).");
            }

            return NoContent();
        }
    }
}
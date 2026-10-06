using freshcrumbs.CRM.domain.entities;
using freshcrumbs.CRM.infrastructure.data;
using Microsoft.EntityFrameworkCore;

namespace freshcrumbs.CRM.api.Services
{
    public interface ITermsService
    {
        // The single published version (null when none has been published).
        Task<TermsVersion?> GetCurrentAsync();

        // The current version when it requires acceptance and the company has not accepted it yet.
        Task<TermsVersion?> GetPendingForCompanyAsync(int companyId);
    }

    public class TermsService : ITermsService
    {
        private readonly MasterCrmDbContext _db;

        public TermsService(MasterCrmDbContext db)
        {
            _db = db;
        }

        public async Task<TermsVersion?> GetCurrentAsync()
        {
            return await _db.TermsVersions
                .AsNoTracking()
                .Where(t => t.Status == TermsStatus.Published)
                .OrderByDescending(t => t.PublishedAt)
                .FirstOrDefaultAsync();
        }

        public async Task<TermsVersion?> GetPendingForCompanyAsync(int companyId)
        {
            var current = await GetCurrentAsync();

            if (current == null || !current.RequiresAcceptance)
            {
                return null;
            }

            var accepted = await _db.TermsAcceptances
                .AnyAsync(a => a.CompanyId == companyId && a.TermsVersionId == current.TermsVersionId);

            return accepted ? null : current;
        }
    }
}
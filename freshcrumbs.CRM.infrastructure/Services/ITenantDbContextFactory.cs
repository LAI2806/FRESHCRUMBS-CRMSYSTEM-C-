using System.Threading.Tasks;
using freshcrumbs.CRM.infrastructure.data;

namespace freshcrumbs.CRM.infrastructure.services
{
    public interface ITenantDbContextFactory
    {
        Task<TenantCrmDbContext> CreateAsync(int companyId);

        // Cloud tenant database described by a mapping (used to check a database before it is mapped).
        TenantCrmDbContext CreateForDatabase(TenantDatabaseInfo databaseInfo);
    }
}
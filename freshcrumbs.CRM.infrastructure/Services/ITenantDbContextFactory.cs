using System.Threading.Tasks;
using freshcrumbs.CRM.infrastructure.data;

namespace freshcrumbs.CRM.infrastructure.services
{
    public interface ITenantDbContextFactory
    {
        Task<TenantCrmDbContext> CreateAsync(int companyId);
    }
}
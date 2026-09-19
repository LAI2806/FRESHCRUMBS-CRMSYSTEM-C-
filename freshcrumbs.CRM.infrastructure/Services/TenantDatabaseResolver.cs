using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using freshcrumbs.CRM.infrastructure.data;

namespace freshcrumbs.CRM.infrastructure.services
{
    public class TenantDatabaseResolver : ITenantDatabaseResolver
    {
        private readonly MasterCrmDbContext _masterDb;

        public TenantDatabaseResolver(MasterCrmDbContext masterDb)
        {
            _masterDb = masterDb;
        }

        public async Task<TenantDatabaseInfo> GetDatabaseInfoAsync(int companyId)
        {
            var tenantDatabase = await _masterDb.CompanyDatabases
                .AsNoTracking()
                .FirstOrDefaultAsync(x =>
                    x.CompanyId == companyId &&
                    x.IsActive);

            if (tenantDatabase == null)
            {
                throw new InvalidOperationException(
                    $"No active tenant database found for CompanyId {companyId}.");
            }

            return new TenantDatabaseInfo
            {
                ServerName = tenantDatabase.ServerName,
                DatabaseName = tenantDatabase.DatabaseName,
                CredentialKey = tenantDatabase.CredentialKey
            };
        }
    }
}
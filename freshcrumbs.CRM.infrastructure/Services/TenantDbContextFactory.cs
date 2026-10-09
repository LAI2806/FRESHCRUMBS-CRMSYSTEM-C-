using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using freshcrumbs.CRM.infrastructure.data;

namespace freshcrumbs.CRM.infrastructure.services
{
    public class TenantDbContextFactory : ITenantDbContextFactory
    {
        private readonly ITenantDatabaseResolver _resolver;
        private readonly IConfiguration _configuration;

        public TenantDbContextFactory(
            ITenantDatabaseResolver resolver,
            IConfiguration configuration)
        {
            _resolver = resolver;
            _configuration = configuration;
        }

        // Local databases already created/migrated by this process (one entry per company).
        private static readonly ConcurrentDictionary<int, Lazy<Task>> LocalDatabasesReady = new();

        // Sync:Mode = Local  -> the desktop's SQL Server Express database is the primary database.
        // Sync:Mode = Cloud (default) -> unchanged behaviour: the MonsterASP tenant database.
        private bool IsLocalMode =>
            string.Equals(_configuration["Sync:Mode"], "Local", StringComparison.OrdinalIgnoreCase);

        public async Task<TenantCrmDbContext> CreateAsync(int companyId)
        {
            if (IsLocalMode)
            {
                return await CreateLocalAsync(companyId);
            }

            var databaseInfo = await _resolver.GetDatabaseInfoAsync(companyId);

            return CreateForDatabase(databaseInfo);
        }

        public TenantCrmDbContext CreateForDatabase(TenantDatabaseInfo databaseInfo)
        {
            var userId = _configuration[
                $"TenantCredentials:{databaseInfo.CredentialKey}:UserId"];

            var password = _configuration[
                $"TenantCredentials:{databaseInfo.CredentialKey}:Password"];

            if (string.IsNullOrWhiteSpace(userId) ||
                string.IsNullOrWhiteSpace(password))
            {
                throw new InvalidOperationException(
                    $"Credentials not found for key '{databaseInfo.CredentialKey}'.");
            }

            var connectionString =
                $"Server={databaseInfo.ServerName};" +
                $"Database={databaseInfo.DatabaseName};" +
                $"User Id={userId};" +
                $"Password={password};" +
                $"Encrypt=True;" +
                $"TrustServerCertificate=True;" +
                $"MultipleActiveResultSets=True;" +
                // Re-open connections the host dropped while idle in the pool (error 19) instead of failing.
                $"ConnectRetryCount=3;" +
                $"ConnectRetryInterval=5;" +
                $"Connect Timeout=30;";

            var options = new DbContextOptionsBuilder<TenantCrmDbContext>()
                .UseSqlServer(connectionString, sqlOptions =>
                    sqlOptions.EnableRetryOnFailure(
                        maxRetryCount: 5,
                        maxRetryDelay: TimeSpan.FromSeconds(10),
                        errorNumbersToAdd: null))
                .Options;

            return new TenantCrmDbContext(options);
        }

        // One local database per tenant: tenant data can never mix between companies.
        private async Task<TenantCrmDbContext> CreateLocalAsync(int companyId)
        {
            var template = _configuration["ConnectionStrings:LocalTenantTemplate"];

            if (string.IsNullOrWhiteSpace(template) || !template.Contains("{database}"))
            {
                throw new InvalidOperationException(
                    "ConnectionStrings:LocalTenantTemplate is missing (it must contain the {database} placeholder).");
            }

            var connectionString = template.Replace("{database}", $"FreshCrumbs_Tenant_{companyId}");

            // No retry strategy on purpose: the local server is on the same machine, and a retry strategy
            // would forbid the user transaction that keeps data + outbox atomic.
            var options = new DbContextOptionsBuilder<TenantCrmDbContext>()
                .UseSqlServer(connectionString)
                .Options;

            var ready = LocalDatabasesReady.GetOrAdd(companyId, _ => new Lazy<Task>(async () =>
            {
                using var migrationContext = new TenantCrmDbContext(options);
                await migrationContext.Database.MigrateAsync();
            }));

            try
            {
                await ready.Value;
            }
            catch
            {
                // Allow a later request to try again (e.g. SQL Server was still starting).
                LocalDatabasesReady.TryRemove(companyId, out _);
                throw;
            }

            return new TenantCrmDbContext(options)
            {
                CaptureSyncChanges = true
            };
        }
    }
}
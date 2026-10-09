using freshcrumbs.CRM.infrastructure.data;
using freshcrumbs.CRM.infrastructure.services;
using freshcrumbs.CRM.infrastructure.services;
using Microsoft.EntityFrameworkCore;
using System;
using freshcrumbs.CRM.domain.entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.Tokens;
using System.Security.Claims;
using System.Text;
using freshcrumbs.CRM.api.Authorization;
using freshcrumbs.CRM.api.Services;
using freshcrumbs.CRM.api.Services.Sync;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Data.SqlClient;

var builder = WebApplication.CreateBuilder(args);

// Sync:Mode = "Cloud" (default) keeps the API exactly as before (cloud databases).
// Sync:Mode = "Local" runs the API on the desktop against SQL Server Express and synchronizes with the cloud API.
var syncOptions = builder.Configuration.GetSection("Sync").Get<SyncOptions>() ?? new SyncOptions();
builder.Services.AddSingleton(syncOptions);

// Remote SQL connections: let SqlClient transparently re-open a connection that the host/network dropped while it
// sat idle in the pool (the "error 19 - Physical connection is not usable" case) instead of failing the request.
static string? WithConnectResilience(string? connectionString)
{
    if (string.IsNullOrWhiteSpace(connectionString))
    {
        return connectionString;
    }

    var csb = new SqlConnectionStringBuilder(connectionString)
    {
        ConnectRetryCount = 3,
        ConnectRetryInterval = 5,
        ConnectTimeout = 30
    };

    return csb.ConnectionString;
}

builder.Services.AddDbContext<MasterCrmDbContext>(options =>
  options.UseSqlServer(
    syncOptions.IsLocal
        ? builder.Configuration.GetConnectionString("LocalMaster")
        : WithConnectResilience(builder.Configuration.GetConnectionString("DB_MasterCRM"))));

builder.Services.AddIdentity<ApplicationUser, IdentityRole>()
    .AddEntityFrameworkStores<MasterCrmDbContext>()
    .AddDefaultTokenProviders();

var jwtKey = builder.Configuration["Jwt:Key"];

if (string.IsNullOrWhiteSpace(jwtKey) || Encoding.UTF8.GetByteCount(jwtKey) < 32)
{
    throw new InvalidOperationException(
        "Jwt:Key is missing or shorter than 32 bytes. Set it with user-secrets or an environment variable (Jwt__Key).");
}

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidateAudience = true,
            ValidAudience = builder.Configuration["Jwt:Audience"],
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(1),
            NameClaimType = ClaimTypes.Name,
            RoleClaimType = ClaimTypes.Role
        };
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy(TenantAccessRequirement.PolicyName, policy =>
        policy.RequireAuthenticatedUser().AddRequirements(new TenantAccessRequirement()));
});

builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton<IAuthorizationHandler, TenantAccessHandler>();

builder.Services.AddDbContext<TenantCrmDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("TenantCrm")));

builder.Services.AddScoped<ITenantDatabaseResolver, TenantDatabaseResolver>();
builder.Services.AddScoped<ITenantDbContextFactory, TenantDbContextFactory>();

builder.Services.AddScoped<CloudSyncApplier>();

if (syncOptions.IsLocal)
{
    // Desktop: users, plan rules and terms come from the snapshot the cloud supplied at the last online
    // validation (see LocalAccessCache), so BASIC/STANDARD/PREMIUM keep being enforced offline.
    builder.Services.AddDataProtection().SetApplicationName("FreshCrumbs.Local");
    builder.Services.AddSingleton<SyncStatusService>();
    builder.Services.AddSingleton<LocalAccessCache>();
    builder.Services.AddSingleton<CloudApiClient>();
    builder.Services.AddSingleton<CloudSessionTokens>();
    builder.Services.AddSingleton<LocalLoginService>();
    builder.Services.AddSingleton<SyncEngine>();
    builder.Services.AddScoped<ISubscriptionService, LocalSubscriptionService>();
    builder.Services.AddScoped<ITermsService, LocalTermsService>();
    builder.Services.AddHostedService<SyncWorker>();
}
else
{
    builder.Services.AddScoped<ISubscriptionService, SubscriptionService>();
    builder.Services.AddScoped<ITermsService, TermsService>();
}

builder.Services.AddControllers(options =>
{
    options.Filters.Add<TenantAccessFilter>();
})
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.ReferenceHandler =
            System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
    });
builder.Services.AddOpenApi();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.UseAuthentication();

app.UseAuthorization();

app.MapControllers();

if (!syncOptions.IsLocal)
{
    // Platform (SuperAdmin) data only exists in the cloud.
    using var scope = app.Services.CreateScope();
    await SuperAdminSeeder.SeedAsync(scope.ServiceProvider);
}

var demoSeedCompanyId = builder.Configuration.GetValue<int?>("DemoSeed:CompanyId");

if (demoSeedCompanyId.HasValue)
{
    using var demoScope = app.Services.CreateScope();
    await TenantDemoDataSeeder.SeedAsync(demoScope.ServiceProvider, demoSeedCompanyId.Value);
}

app.Run();
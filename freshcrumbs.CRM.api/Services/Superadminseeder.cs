using freshcrumbs.CRM.domain.entities;
using Microsoft.AspNetCore.Identity;

namespace freshcrumbs.CRM.api.Services
{
    public static class SuperAdminSeeder
    {
        public static async Task SeedAsync(IServiceProvider services)
        {
            var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger("SuperAdminSeeder");
            var configuration = services.GetRequiredService<IConfiguration>();

            try
            {
                var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();

                if (!await roleManager.RoleExistsAsync(PlatformRoles.SuperAdmin))
                {
                    await roleManager.CreateAsync(new IdentityRole(PlatformRoles.SuperAdmin));
                }

                var userName = configuration["SuperAdminSeed:UserName"];
                var email = configuration["SuperAdminSeed:Email"];
                var password = configuration["SuperAdminSeed:Password"];

                if (string.IsNullOrWhiteSpace(userName)
                    || string.IsNullOrWhiteSpace(email)
                    || string.IsNullOrWhiteSpace(password))
                {
                    logger.LogInformation("SuperAdminSeed credentials are not configured. No Super Admin user was created.");
                    return;
                }

                var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
                var user = await userManager.FindByNameAsync(userName);

                if (user == null)
                {
                    user = new ApplicationUser
                    {
                        UserName = userName,
                        Email = email,
                        EmailConfirmed = true,
                        TenantId = null,
                        FirstName = configuration["SuperAdminSeed:FirstName"] ?? "Super",
                        LastName = configuration["SuperAdminSeed:LastName"] ?? "Admin",
                        Role = PlatformRoles.SuperAdmin,
                        Status = "Active"
                    };

                    var created = await userManager.CreateAsync(user, password);

                    if (!created.Succeeded)
                    {
                        logger.LogError("Super Admin user could not be created: {Errors}",
                            string.Join("; ", created.Errors.Select(e => e.Description)));
                        return;
                    }
                }

                if (!await userManager.IsInRoleAsync(user, PlatformRoles.SuperAdmin))
                {
                    await userManager.AddToRoleAsync(user, PlatformRoles.SuperAdmin);
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Super Admin seeding was skipped. Make sure the AddSuperAdminAndPlans migration has been applied to DB_MasterCRM.");
            }
        }
    }
}
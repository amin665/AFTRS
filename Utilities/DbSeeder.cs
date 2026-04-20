using Microsoft.AspNetCore.Identity;

namespace AFTRS.Utilities;

public static class DbSeeder
{
    public static async Task SeedRolesAndAdminAsync(IServiceProvider service)
    {
        // Seed Roles
        var roleManager = service.GetRequiredService<RoleManager<IdentityRole>>();
        var userManager = service.GetRequiredService<UserManager<IdentityUser>>();

        string[] roles = { "Admin", "FinancialManager" };

        foreach (var role in roles)
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                await roleManager.CreateAsync(new IdentityRole(role));
            }
        }

        // Seed a default Admin User for you to test with
        var adminEmail = "admin@aftrs.com";
        var adminUser = await userManager.FindByEmailAsync(adminEmail);

        if (adminUser == null)
        {
            adminUser = new IdentityUser
            {
                UserName = adminEmail,
                Email = adminEmail,
                EmailConfirmed = true
            };
            await userManager.CreateAsync(adminUser, "Admin123!"); // Change this later!
            await userManager.AddToRoleAsync(adminUser, "Admin");
        }
    }
}
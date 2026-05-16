using Microsoft.AspNetCore.Identity;

namespace web.Data;

public static class IdentitySeeder
{
    private static readonly string[] Roles = ["Administrator", "Koordinator"];

    public static async Task SeedRolesAsync(RoleManager<IdentityRole> roleManager)
    {
        // Fjern forældede roller
        var obsoleteRoles = new[] { "Bruger" };
        foreach (var obsolete in obsoleteRoles)
        {
            var existing = await roleManager.FindByNameAsync(obsolete);
            if (existing != null)
                await roleManager.DeleteAsync(existing);
        }

        foreach (var role in Roles)
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                await roleManager.CreateAsync(new IdentityRole(role));
            }
        }
    }
}

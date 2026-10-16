using AcxiomCrm.Domain.Security;
using Microsoft.AspNetCore.Identity;

namespace AcxiomCrm.Infrastructure.Identity;

public sealed class RoleSeeder(RoleManager<ApplicationRole> roleManager)
{
    public async Task SeedAsync(CancellationToken ct = default)
    {
        foreach (var role in Roles.All)
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                var appRole = new ApplicationRole
                {
                    Name = role,
                    NormalizedName = role.ToUpperInvariant()
                };
                var result = await roleManager.CreateAsync(appRole);
                if (!result.Succeeded)
                {
                    var errors = string.Join("; ", result.Errors.Select(e => e.Description));
                    throw new InvalidOperationException($"Failed to create role '{role}': {errors}");
                }
            }
        }
    }
}

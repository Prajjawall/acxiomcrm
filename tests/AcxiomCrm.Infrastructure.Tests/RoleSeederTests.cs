using AcxiomCrm.Domain.Security;
using AcxiomCrm.Infrastructure.Identity;
using AcxiomCrm.TestSupport;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Shouldly;

namespace AcxiomCrm.Infrastructure.Tests;

public class RoleSeederTests : DbTestBase
{
    public RoleSeederTests(SqlServerContainerFixture fixture) : base(fixture)
    {
    }

    [Fact]
    public async Task Seeds_three_roles_once()
    {
        var services = new ServiceCollection();
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Default"] = Db.ConnectionString
            })
            .Build();

        services.AddLogging(b => b.AddDebug());
        services.AddSingleton<IConfiguration>(config);
        services.AddInfrastructure(config);

        await using var sp = services.BuildServiceProvider();
        using (var scope = sp.CreateScope())
        {
            var seeder = scope.ServiceProvider.GetRequiredService<RoleSeeder>();
            await seeder.SeedAsync();
        }

        // Run second time to verify idempotency
        using (var scope = sp.CreateScope())
        {
            var seeder = scope.ServiceProvider.GetRequiredService<RoleSeeder>();
            await seeder.SeedAsync();
        }

        using (var scope = sp.CreateScope())
        {
            var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<ApplicationRole>>();
            var roles = await roleManager.Roles.ToListAsync();

            roles.Count.ShouldBe(3);
            roles.Select(r => r.Name).ShouldBe(Roles.All, ignoreOrder: true);
        }
    }
}

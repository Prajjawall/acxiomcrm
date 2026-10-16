using AcxiomCrm.Domain.Security;
using AcxiomCrm.Infrastructure.Identity;
using AcxiomCrm.TestSupport;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Hosting.Internal;
using Microsoft.Extensions.Logging;
using Shouldly;

namespace AcxiomCrm.Infrastructure.Tests;

public class BootstrapAdminTests : DbTestBase
{
    public BootstrapAdminTests(SqlServerContainerFixture fixture) : base(fixture)
    {
    }

    private ServiceProvider CreateServices(
        Dictionary<string, string?>? configValues = null,
        string environmentName = "Development")
    {
        var services = new ServiceCollection();

        var initial = new Dictionary<string, string?>
        {
            ["ConnectionStrings:Default"] = Db.ConnectionString,
            ["Security:Password:MinLength"] = "8",
            ["Bootstrap:AdminEmail"] = "admin@acxiom.com",
            ["Bootstrap:AdminPassword"] = "Adm1n-Str0ng-2026!",
            ["Bootstrap:AdminFullName"] = "Admin User"
        };

        if (configValues != null)
        {
            foreach (var (k, v) in configValues)
            {
                initial[k] = v;
            }
        }

        var config = new ConfigurationBuilder().AddInMemoryCollection(initial).Build();

        services.AddLogging(b => b.AddDebug());
        services.AddSingleton<IConfiguration>(config);
        services.AddSingleton<IHostEnvironment>(new HostingEnvironment { EnvironmentName = environmentName });
        services.AddInfrastructure(config);

        return services.BuildServiceProvider();
    }

    [Fact]
    public async Task Creates_admin_when_none_exists()
    {
        await using var sp = CreateServices();
        using var scope = sp.CreateScope();

        var roleSeeder = scope.ServiceProvider.GetRequiredService<RoleSeeder>();
        await roleSeeder.SeedAsync();

        var adminSeeder = scope.ServiceProvider.GetRequiredService<BootstrapAdminSeeder>();
        await adminSeeder.SeedAsync();

        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var adminUsers = await userManager.GetUsersInRoleAsync(Roles.Admin);

        adminUsers.Count.ShouldBe(1);
        adminUsers[0].Email.ShouldBe("admin@acxiom.com");
        adminUsers[0].FullName.ShouldBe("Admin User");
        adminUsers[0].IsActive.ShouldBeTrue();
    }

    [Fact]
    public async Task Does_not_create_second_admin()
    {
        await using var sp = CreateServices();
        using var scope = sp.CreateScope();

        var roleSeeder = scope.ServiceProvider.GetRequiredService<RoleSeeder>();
        await roleSeeder.SeedAsync();

        var adminSeeder = scope.ServiceProvider.GetRequiredService<BootstrapAdminSeeder>();
        await adminSeeder.SeedAsync();
        await adminSeeder.SeedAsync(); // Run again

        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var adminUsers = await userManager.GetUsersInRoleAsync(Roles.Admin);

        adminUsers.Count.ShouldBe(1);
    }

    [Fact]
    public async Task Weak_password_throws_without_echoing_password()
    {
        const string weakPassword = "weak";
        await using var sp = CreateServices(new Dictionary<string, string?>
        {
            ["Bootstrap:AdminPassword"] = weakPassword
        });
        using var scope = sp.CreateScope();

        var roleSeeder = scope.ServiceProvider.GetRequiredService<RoleSeeder>();
        await roleSeeder.SeedAsync();

        var adminSeeder = scope.ServiceProvider.GetRequiredService<BootstrapAdminSeeder>();
        var ex = await Should.ThrowAsync<InvalidOperationException>(() => adminSeeder.SeedAsync());

        ex.Message.ShouldNotContain(weakPassword);
    }

    [Fact]
    public async Task Missing_config_in_Production_throws()
    {
        await using var sp = CreateServices(
            new Dictionary<string, string?>
            {
                ["Bootstrap:AdminEmail"] = null,
                ["Bootstrap:AdminPassword"] = null
            },
            environmentName: "Production");

        using var scope = sp.CreateScope();
        var adminSeeder = scope.ServiceProvider.GetRequiredService<BootstrapAdminSeeder>();

        await Should.ThrowAsync<InvalidOperationException>(() => adminSeeder.SeedAsync());
    }
}

using AcxiomCrm.Infrastructure.Identity;
using AcxiomCrm.TestSupport;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Shouldly;

namespace AcxiomCrm.Infrastructure.Tests;

public class PasswordPolicyTests : DbTestBase
{
    public PasswordPolicyTests(SqlServerContainerFixture fixture) : base(fixture)
    {
    }

    private ServiceProvider CreateServices(Dictionary<string, string?>? configOverrides = null)
    {
        var services = new ServiceCollection();

        var configBuilder = new ConfigurationBuilder();
        var initialData = new Dictionary<string, string?>
        {
            ["ConnectionStrings:Default"] = Db.ConnectionString,
            ["Security:Password:MinLength"] = "8",
            ["Security:Password:RequireUpper"] = "true",
            ["Security:Password:RequireLower"] = "true",
            ["Security:Password:RequireDigit"] = "true",
            ["Security:Password:RequireSymbol"] = "true"
        };

        if (configOverrides != null)
        {
            foreach (var (k, v) in configOverrides)
            {
                initialData[k] = v;
            }
        }

        var config = configBuilder.AddInMemoryCollection(initialData).Build();

        services.AddLogging(b => b.AddDebug());
        services.AddSingleton<IConfiguration>(config);
        services.AddInfrastructure(config);

        return services.BuildServiceProvider();
    }

    [Theory]
    [InlineData("Sh0rt!a", false)] // < 8
    [InlineData("alllowercase123!", false)] // no upper
    [InlineData("ALLUPPERCASE123!", false)] // no lower
    [InlineData("NoDigitsHere!!!!", false)] // no digit
    [InlineData("NoSymbolsHere1234", false)] // no symbol
    [InlineData("Password123!", false)] // deny-list
    [InlineData("Tr1cky-Harbour-77!", true)] // valid
    public async Task Password_rules_are_enforced(string password, bool expectedSuccess)
    {
        await using var sp = CreateServices();
        using var scope = sp.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        var user = new ApplicationUser
        {
            UserName = "test.user",
            Email = "test.user@example.com",
            FullName = "Test User"
        };

        var result = await userManager.CreateAsync(user, password);

        result.Succeeded.ShouldBe(expectedSuccess);
    }

    [Fact]
    public async Task Password_containing_username_fails()
    {
        await using var sp = CreateServices();
        using var scope = sp.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        var user = new ApplicationUser
        {
            UserName = "asha.rao",
            Email = "asha@example.com",
            FullName = "Asha Rao"
        };

        var result = await userManager.CreateAsync(user, "Asha.Rao-2026!x");

        result.Succeeded.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.Code == "PasswordContainsUserName");
    }

    [Fact]
    public async Task Production_config_requires_min_length_12()
    {
        await using var sp = CreateServices(new Dictionary<string, string?>
        {
            ["Security:Password:MinLength"] = "12"
        });
        using var scope = sp.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        var user1 = new ApplicationUser { UserName = "u1", Email = "u1@example.com" };
        var result11 = await userManager.CreateAsync(user1, "Tr1cky!1234"); // 11 chars
        result11.Succeeded.ShouldBeFalse();

        var user2 = new ApplicationUser { UserName = "u2", Email = "u2@example.com" };
        var result12 = await userManager.CreateAsync(user2, "Tr1cky!12345"); // 12 chars
        result12.Succeeded.ShouldBeTrue();
    }
}

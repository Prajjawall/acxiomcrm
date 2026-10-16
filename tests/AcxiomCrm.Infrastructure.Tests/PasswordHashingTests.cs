using AcxiomCrm.Infrastructure.Identity;
using AcxiomCrm.TestSupport;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Shouldly;

namespace AcxiomCrm.Infrastructure.Tests;

public class PasswordHashingTests : DbTestBase
{
    public PasswordHashingTests(SqlServerContainerFixture fixture) : base(fixture)
    {
    }

    private ServiceProvider CreateServices()
    {
        var services = new ServiceCollection();

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Default"] = Db.ConnectionString,
                ["Security:Password:MinLength"] = "8"
            })
            .Build();

        services.AddLogging(b => b.AddDebug());
        services.AddSingleton<IConfiguration>(config);
        services.AddInfrastructure(config);

        return services.BuildServiceProvider();
    }

    [Fact]
    public async Task Stored_hash_is_not_plaintext_and_is_identity_v3()
    {
        await using var sp = CreateServices();
        using var scope = sp.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher<ApplicationUser>>();

        var plainTextPassword = "Str0ng-Password-77!";
        var user = new ApplicationUser
        {
            UserName = "hash.test",
            Email = "hash.test@example.com"
        };

        var result = await userManager.CreateAsync(user, plainTextPassword);
        result.Succeeded.ShouldBeTrue();

        user.PasswordHash.ShouldNotBeNull();
        user.PasswordHash.ShouldNotBe(plainTextPassword);

        var bytes = Convert.FromBase64String(user.PasswordHash);
        bytes[0].ShouldBe((byte)0x01); // Identity v3 marker

        var verificationResult = hasher.VerifyHashedPassword(user, user.PasswordHash, plainTextPassword);
        verificationResult.ShouldBe(PasswordVerificationResult.Success);
    }

    [Fact]
    public async Task Iteration_count_is_600000()
    {
        await using var sp = CreateServices();
        using var scope = sp.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        var user = new ApplicationUser
        {
            UserName = "iteration.test",
            Email = "iteration.test@example.com"
        };

        var result = await userManager.CreateAsync(user, "Str0ng-Password-77!");
        result.Succeeded.ShouldBeTrue();

        var bytes = Convert.FromBase64String(user.PasswordHash!);
        // Bytes 5-8 are iteration count in big-endian
        var iterationCount = (bytes[5] << 24) | (bytes[6] << 16) | (bytes[7] << 8) | bytes[8];
        iterationCount.ShouldBe(600_000);
    }
}

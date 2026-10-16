using AcxiomCrm.Infrastructure.Options;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Shouldly;

namespace AcxiomCrm.Infrastructure.Tests;

public class OptionsValidationTests
{
    private static ServiceProvider BuildServices(Dictionary<string, string?> inMemorySettings)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(inMemorySettings)
            .Build();

        var services = new ServiceCollection();
        services.AddInfrastructure(config);
        return services.BuildServiceProvider();
    }

    [Fact]
    public void Defaults_are_valid()
    {
        var sp = BuildServices(new Dictionary<string, string?>());

        // Resolving validated options triggers validation
        Should.NotThrow(() => sp.GetRequiredService<IOptions<BusinessOptions>>().Value);
        Should.NotThrow(() => sp.GetRequiredService<IOptions<LockoutOptions>>().Value);
        Should.NotThrow(() => sp.GetRequiredService<IOptions<PasswordPolicyOptions>>().Value);
        Should.NotThrow(() => sp.GetRequiredService<IOptions<RegistrationOptions>>().Value);
        Should.NotThrow(() => sp.GetRequiredService<IOptions<AuditOptions>>().Value);
        Should.NotThrow(() => sp.GetRequiredService<IOptions<DatabaseOptions>>().Value);
    }

    [Theory]
    [InlineData("1")] // < 3
    [InlineData("25")] // > 20
    public void Lockout_threshold_out_of_range_fails_validation(string maxFailedAttempts)
    {
        var sp = BuildServices(new Dictionary<string, string?>
        {
            ["Security:Lockout:MaxFailedAttempts"] = maxFailedAttempts
        });

        Should.Throw<OptionsValidationException>(() => sp.GetRequiredService<IOptions<LockoutOptions>>().Value);
    }

    [Fact]
    public void Business_timezone_unknown_fails_on_start()
    {
        var sp = BuildServices(new Dictionary<string, string?>
        {
            ["Business:TimeZone"] = "Not/A_Real_TimeZone"
        });

        Should.Throw<OptionsValidationException>(() => sp.GetRequiredService<IOptions<BusinessOptions>>().Value);
    }
}

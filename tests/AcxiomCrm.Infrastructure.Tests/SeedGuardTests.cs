using AcxiomCrm.Infrastructure.Options;
using AcxiomCrm.Infrastructure.Seeding;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Hosting.Internal;

namespace AcxiomCrm.Infrastructure.Tests;

public sealed class SeedGuardTests
{
    [Fact]
    public void Throws_When_Production_And_SeedDemoData_Is_True()
    {
        var env = new HostingEnvironment { EnvironmentName = Environments.Production };
        var options = new DatabaseOptions { SeedDemoData = true };

        var ex = Assert.Throws<InvalidOperationException>(() => SeedGuard.EnsureAllowed(env, options));
        Assert.Equal("Demo data seeding is not permitted in Production.", ex.Message);
    }

    [Fact]
    public void Allowed_When_Production_And_SeedDemoData_Is_False()
    {
        var env = new HostingEnvironment { EnvironmentName = Environments.Production };
        var options = new DatabaseOptions { SeedDemoData = false };

        SeedGuard.EnsureAllowed(env, options);
    }

    [Fact]
    public void Allowed_When_Development_And_SeedDemoData_Is_True()
    {
        var env = new HostingEnvironment { EnvironmentName = Environments.Development };
        var options = new DatabaseOptions { SeedDemoData = true };

        SeedGuard.EnsureAllowed(env, options);
    }
}

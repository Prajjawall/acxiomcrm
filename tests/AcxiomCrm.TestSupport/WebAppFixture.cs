using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace AcxiomCrm.TestSupport;

public class WebAppFixture : WebApplicationFactory<Program>, IAsyncLifetime
{
    private static readonly Lazy<TestDatabase> LazyDatabase = new(() =>
    {
        var fixture = new SqlServerContainerFixture();
        fixture.InitializeAsync().GetAwaiter().GetResult();
        return fixture.CreateDatabaseAsync().GetAwaiter().GetResult();
    });

    public static string ConnectionString => LazyDatabase.Value.ConnectionString;

    public Task InitializeAsync()
    {
        _ = LazyDatabase.Value;
        return Task.CompletedTask;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:Default", ConnectionString);
        builder.UseSetting("Database:MigrateOnStartup", "true");
    }

    public new async Task DisposeAsync()
    {
        await base.DisposeAsync();
    }
}

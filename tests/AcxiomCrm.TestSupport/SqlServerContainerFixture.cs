using Testcontainers.MsSql;
using Xunit;

namespace AcxiomCrm.TestSupport;

public sealed class SqlServerContainerFixture : IAsyncLifetime
{
    private MsSqlContainer? _container;

    public string AdminConnectionString { get; private set; } = string.Empty;

    public async Task InitializeAsync()
    {
        var existingConn = Environment.GetEnvironmentVariable("TEST_DB_CONNECTION");
        if (!string.IsNullOrWhiteSpace(existingConn))
        {
            AdminConnectionString = existingConn;
            return;
        }

        _container = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest")
            .Build();

        await _container.StartAsync();
        AdminConnectionString = _container.GetConnectionString();
    }

    public async Task<TestDatabase> CreateDatabaseAsync()
    {
        var dbName = $"acxiomcrm_test_{Guid.NewGuid():N}";
        var testDb = new TestDatabase(AdminConnectionString, dbName);
        await testDb.InitializeAsync();
        return testDb;
    }

    public async Task DisposeAsync()
    {
        if (_container != null)
        {
            await _container.DisposeAsync();
        }
    }
}

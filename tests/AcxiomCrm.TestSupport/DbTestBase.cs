using Xunit;

namespace AcxiomCrm.TestSupport;

[Collection("sql")]
public abstract class DbTestBase(SqlServerContainerFixture fixture) : IAsyncLifetime
{
    private TestDatabase? _db;

    public TestDatabase Db => _db ?? throw new InvalidOperationException("Database has not been initialized.");

    public virtual async Task InitializeAsync()
    {
        _db = await fixture.CreateDatabaseAsync();
    }

    public virtual async Task DisposeAsync()
    {
        if (_db != null)
        {
            await _db.DisposeAsync();
        }
    }
}

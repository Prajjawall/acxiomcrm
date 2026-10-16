using AcxiomCrm.Application.Abstractions;
using AcxiomCrm.Infrastructure.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Respawn;

namespace AcxiomCrm.TestSupport;

public sealed class TestDatabase : IAsyncDisposable
{
    private readonly string _adminConnectionString;
    private readonly string _databaseName;
    private Respawner? _respawner;

    public string ConnectionString { get; }

    public TestDatabase(string adminConnectionString, string databaseName)
    {
        _adminConnectionString = adminConnectionString;
        _databaseName = databaseName;

        var builder = new SqlConnectionStringBuilder(adminConnectionString)
        {
            InitialCatalog = databaseName
        };
        ConnectionString = builder.ConnectionString;
    }

    public async Task InitializeAsync()
    {
        await using (var connection = new SqlConnection(_adminConnectionString))
        {
            await connection.OpenAsync();
            await using var cmd = connection.CreateCommand();
            cmd.CommandText = $"CREATE DATABASE [{_databaseName}]";
            await cmd.ExecuteNonQueryAsync();
        }

        await using (var context = CreateContext())
        {
            await context.Database.MigrateAsync();
        }

        await using (var connection = new SqlConnection(ConnectionString))
        {
            await connection.OpenAsync();
            _respawner = await Respawner.CreateAsync(connection, new RespawnerOptions
            {
                TablesToIgnore = ["__EFMigrationsHistory", "AspNetRoles"],
                SchemasToInclude = ["dbo"]
            });
        }
    }

    public CrmDbContext CreateContext(IClock? clock = null, ICurrentUser? user = null)
    {
        var options = new DbContextOptionsBuilder<CrmDbContext>()
            .UseSqlServer(ConnectionString)
            .Options;

        return new CrmDbContext(options);
    }

    public async Task ResetAsync()
    {
        if (_respawner != null)
        {
            await using var connection = new SqlConnection(ConnectionString);
            await connection.OpenAsync();
            await _respawner.ResetAsync(connection);
        }
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            await using var connection = new SqlConnection(_adminConnectionString);
            await connection.OpenAsync();
            await using var cmd = connection.CreateCommand();
            cmd.CommandText = $"""
                IF EXISTS (SELECT name FROM sys.databases WHERE name = '{_databaseName}')
                BEGIN
                    ALTER DATABASE [{_databaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
                    DROP DATABASE [{_databaseName}];
                END
                """;
            await cmd.ExecuteNonQueryAsync();
        }
        catch
        {
            // Suppress error on cleanup if server is already stopped
        }
    }
}

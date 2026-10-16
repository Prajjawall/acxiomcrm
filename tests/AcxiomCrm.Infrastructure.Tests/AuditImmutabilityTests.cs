using AcxiomCrm.TestSupport;
using Microsoft.Data.SqlClient;
using Shouldly;

namespace AcxiomCrm.Infrastructure.Tests;

public class AuditImmutabilityTests : DbTestBase
{
    public AuditImmutabilityTests(SqlServerContainerFixture fixture) : base(fixture)
    {
    }

    [Fact]
    public async Task Insert_succeeds()
    {
        await using var connection = new SqlConnection(Db.ConnectionString);
        await connection.OpenAsync();

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            INSERT INTO AuditLogs (CreatedDate, Module, Action, EntityName, Result)
            VALUES (SYSUTCDATETIME(), 'Authentication', 'Login', 'User', 1);
            """;

        var rows = await cmd.ExecuteNonQueryAsync();
        rows.ShouldBe(1);
    }

    [Fact]
    public async Task Update_via_sql_fails()
    {
        await using var connection = new SqlConnection(Db.ConnectionString);
        await connection.OpenAsync();

        await using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = """
                INSERT INTO AuditLogs (CreatedDate, Module, Action, EntityName, Result)
                VALUES (SYSUTCDATETIME(), 'Authentication', 'Login', 'User', 1);
                """;
            await cmd.ExecuteNonQueryAsync();
        }

        await using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = "UPDATE AuditLogs SET Details = 'Modified';";
            var ex = await Should.ThrowAsync<SqlException>(() => cmd.ExecuteNonQueryAsync());
            ex.Number.ShouldBe(51000);
            ex.Message.ShouldContain("AuditLogs is append-only.");
        }
    }

    [Fact]
    public async Task Delete_via_sql_fails()
    {
        await using var connection = new SqlConnection(Db.ConnectionString);
        await connection.OpenAsync();

        await using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = """
                INSERT INTO AuditLogs (CreatedDate, Module, Action, EntityName, Result)
                VALUES (SYSUTCDATETIME(), 'Authentication', 'Login', 'User', 1);
                """;
            await cmd.ExecuteNonQueryAsync();
        }

        await using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = "DELETE FROM AuditLogs;";
            var ex = await Should.ThrowAsync<SqlException>(() => cmd.ExecuteNonQueryAsync());
            ex.Number.ShouldBe(51000);
            ex.Message.ShouldContain("AuditLogs is append-only.");
        }
    }
}

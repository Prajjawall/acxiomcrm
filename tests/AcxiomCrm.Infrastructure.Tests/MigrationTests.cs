using AcxiomCrm.TestSupport;
using Microsoft.Data.SqlClient;
using Shouldly;

namespace AcxiomCrm.Infrastructure.Tests;

public class MigrationTests(SqlServerContainerFixture fixture) : DbTestBase(fixture)
{
    private sealed record ColumnInfo(string ColumnName, string DataType, int? MaxLength, string IsNullable);

    [Fact]
    public async Task Initial_migration_creates_identity_tables_and_ApplicationUser_columns()
    {
        await using var connection = new SqlConnection(Db.ConnectionString);
        await connection.OpenAsync();

        // Query columns for AspNetUsers
        var columns = new List<ColumnInfo>();
        await using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = """
                SELECT COLUMN_NAME, DATA_TYPE, CHARACTER_MAXIMUM_LENGTH, IS_NULLABLE
                FROM INFORMATION_SCHEMA.COLUMNS
                WHERE TABLE_NAME = 'AspNetUsers'
                """;

            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                columns.Add(new ColumnInfo(
                    reader.GetString(0),
                    reader.GetString(1),
                    reader.IsDBNull(2) ? null : reader.GetInt32(2),
                    reader.GetString(3)));
            }
        }

        // Verify FullName nvarchar(150) NOT NULL
        var fullName = columns.FirstOrDefault(c => c.ColumnName == "FullName");
        fullName.ShouldNotBeNull();
        fullName.DataType.ShouldBe("nvarchar");
        fullName.MaxLength.ShouldBe(150);
        fullName.IsNullable.ShouldBe("NO");

        // Verify IsActive bit NOT NULL
        var isActive = columns.FirstOrDefault(c => c.ColumnName == "IsActive");
        isActive.ShouldNotBeNull();
        isActive.DataType.ShouldBe("bit");
        isActive.IsNullable.ShouldBe("NO");

        // Verify ManagerId nvarchar(450) NULL
        var managerId = columns.FirstOrDefault(c => c.ColumnName == "ManagerId");
        managerId.ShouldNotBeNull();
        managerId.DataType.ShouldBe("nvarchar");
        managerId.MaxLength.ShouldBe(450);
        managerId.IsNullable.ShouldBe("YES");

        // Verify MustChangePassword bit NOT NULL
        var mustChangePassword = columns.FirstOrDefault(c => c.ColumnName == "MustChangePassword");
        mustChangePassword.ShouldNotBeNull();
        mustChangePassword.DataType.ShouldBe("bit");
        mustChangePassword.IsNullable.ShouldBe("NO");

        // Verify CreatedDate datetime2 NOT NULL
        var createdDate = columns.FirstOrDefault(c => c.ColumnName == "CreatedDate");
        createdDate.ShouldNotBeNull();
        createdDate.DataType.ShouldBe("datetime2");
        createdDate.IsNullable.ShouldBe("NO");

        // Verify LastLoginDate datetime2 NULL
        var lastLoginDate = columns.FirstOrDefault(c => c.ColumnName == "LastLoginDate");
        lastLoginDate.ShouldNotBeNull();
        lastLoginDate.DataType.ShouldBe("datetime2");
        lastLoginDate.IsNullable.ShouldBe("YES");

        // Assert CHECK constraint rejects ManagerId = Id (SqlException 547)
        var userId = Guid.NewGuid().ToString();
        var ex = await Should.ThrowAsync<SqlException>(async () =>
        {
            await using var insertCmd = connection.CreateCommand();
            insertCmd.CommandText = $"""
                INSERT INTO AspNetUsers (Id, UserName, FullName, IsActive, ManagerId, MustChangePassword, CreatedDate, AccessFailedCount, EmailConfirmed, PhoneNumberConfirmed, TwoFactorEnabled, LockoutEnabled)
                VALUES ('{userId}', 'selfmanager', 'Self Manager', 1, '{userId}', 0, SYSUTCDATETIME(), 0, 0, 0, 0, 1)
                """;
            await insertCmd.ExecuteNonQueryAsync();
        });

        ex.Number.ShouldBe(547); // 547 is constraint violation
    }
}

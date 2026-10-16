using AcxiomCrm.TestSupport;
using Microsoft.Data.SqlClient;
using Shouldly;

namespace AcxiomCrm.Infrastructure.Tests;

public class AuditSchemaTests : DbTestBase
{
    public AuditSchemaTests(SqlServerContainerFixture fixture) : base(fixture)
    {
    }

    [Fact]
    public async Task Columns_and_indexes_match_data_model()
    {
        await using var connection = new SqlConnection(Db.ConnectionString);
        await connection.OpenAsync();

        var columns = new Dictionary<string, (string DataType, int? MaxLength, bool IsNullable)>(StringComparer.OrdinalIgnoreCase);

        await using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = """
                SELECT COLUMN_NAME, DATA_TYPE, CHARACTER_MAXIMUM_LENGTH, IS_NULLABLE
                FROM INFORMATION_SCHEMA.COLUMNS
                WHERE TABLE_NAME = 'AuditLogs'
                """;

            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var colName = reader.GetString(0);
                var dataType = reader.GetString(1);
                var maxLen = reader.IsDBNull(2) ? (int?)null : reader.GetInt32(2);
                var isNullable = reader.GetString(3) == "YES";
                columns[colName] = (dataType, maxLen, isNullable);
            }
        }

        columns.ContainsKey("AuditLogId").ShouldBeTrue();
        columns["AuditLogId"].DataType.ShouldBe("bigint");
        columns["AuditLogId"].IsNullable.ShouldBeFalse();

        columns.ContainsKey("CreatedDate").ShouldBeTrue();
        columns["CreatedDate"].DataType.ShouldBe("datetime2");
        columns["CreatedDate"].IsNullable.ShouldBeFalse();

        columns.ContainsKey("Module").ShouldBeTrue();
        columns["Module"].MaxLength.ShouldBe(50);
        columns["Module"].IsNullable.ShouldBeFalse();

        columns.ContainsKey("Action").ShouldBeTrue();
        columns["Action"].MaxLength.ShouldBe(50);
        columns["Action"].IsNullable.ShouldBeFalse();

        columns.ContainsKey("EntityName").ShouldBeTrue();
        columns["EntityName"].MaxLength.ShouldBe(100);
        columns["EntityName"].IsNullable.ShouldBeFalse();

        columns.ContainsKey("Result").ShouldBeTrue();
        columns["Result"].DataType.ShouldBe("tinyint");
        columns["Result"].IsNullable.ShouldBeFalse();

        columns.ContainsKey("Details").ShouldBeTrue();
        columns["Details"].MaxLength.ShouldBe(1000);
        columns["Details"].IsNullable.ShouldBeTrue();

        // Indexes check
        var indexes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        await using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = """
                SELECT name FROM sys.indexes
                WHERE object_id = OBJECT_ID('AuditLogs') AND name IS NOT NULL
                """;

            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                indexes.Add(reader.GetString(0));
            }
        }

        indexes.ShouldContain("IX_AuditLogs_CreatedDate");
        indexes.ShouldContain("IX_AuditLogs_UserId_CreatedDate");
        indexes.ShouldContain("IX_AuditLogs_Module_Action_CreatedDate");
        indexes.ShouldContain("IX_AuditLogs_EntityName_RecordId");
    }
}

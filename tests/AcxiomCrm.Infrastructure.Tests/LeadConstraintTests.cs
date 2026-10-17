using AcxiomCrm.Domain.Entities;
using AcxiomCrm.Domain.Enums;
using AcxiomCrm.Domain.Validation;
using AcxiomCrm.TestSupport;
using Microsoft.Data.SqlClient;
using Shouldly;

namespace AcxiomCrm.Infrastructure.Tests;

public class LeadConstraintTests : DbTestBase
{
    private static readonly DateTime FixedDate = new(2026, 10, 16, 12, 0, 0, DateTimeKind.Utc);

    public LeadConstraintTests(SqlServerContainerFixture fixture) : base(fixture)
    {
    }

    private async Task<string> SeedUserAsync()
    {
        var context = Db.CreateContext();
        var user = new Identity.ApplicationUser
        {
            Id = Guid.NewGuid().ToString(),
            UserName = "testexec" + Guid.NewGuid().ToString("N")[..6],
            Email = "exec" + Guid.NewGuid().ToString("N")[..6] + "@example.com",
            FullName = "Test Executive",
            IsActive = true,
            CreatedDate = FixedDate
        };
        context.Users.Add(user);
        await context.SaveChangesAsync();
        return user.Id;
    }

    private async Task<int> SeedCustomerAsync(string ownerId)
    {
        var context = Db.CreateContext();
        var customer = Customer.Create(
            "Test Customer",
            null,
            "cust" + Guid.NewGuid().ToString("N")[..6] + "@example.com",
            "9876543210",
            null,
            null,
            null,
            CustomerStatus.Active,
            ownerId,
            null
        );
        context.Customers.Add(customer);
        await context.SaveChangesAsync();
        return customer.Id;
    }

    [Fact]
    public async Task Code_is_generated_as_LEAD_000001_and_increments()
    {
        var ownerId = await SeedUserAsync();
        var context = Db.CreateContext();

        var l1 = Lead.Create("Lead One", "Company 1", "l1@example.com", null, LeadSource.Website, LeadPriority.Low, 1000m, ownerId, null);
        var l2 = Lead.Create("Lead Two", "Company 2", null, "9876543210", LeadSource.Referral, LeadPriority.High, 2000m, ownerId, null);

        context.Leads.AddRange(l1, l2);
        await context.SaveChangesAsync();

        l1.Code.ShouldStartWith("LEAD-");
        l2.Code.ShouldStartWith("LEAD-");
        l1.Code.Length.ShouldBe(11); // LEAD-000001
        l2.Code.Length.ShouldBe(11);
    }

    [Fact]
    public async Task Column_lengths_and_index_names_match()
    {
        await using var connection = new SqlConnection(Db.ConnectionString);
        await connection.OpenAsync();

        var columns = new Dictionary<string, (string DataType, int? MaxLength, bool IsNullable)>(StringComparer.OrdinalIgnoreCase);

        await using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = """
                SELECT COLUMN_NAME, DATA_TYPE, CHARACTER_MAXIMUM_LENGTH, IS_NULLABLE
                FROM INFORMATION_SCHEMA.COLUMNS
                WHERE TABLE_NAME = 'Leads'
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

        columns.ContainsKey("LeadId").ShouldBeTrue();
        columns["LeadName"].MaxLength.ShouldBe(Limits.Name);
        columns["CompanyName"].MaxLength.ShouldBe(Limits.Name);
        columns["Email"].MaxLength.ShouldBe(Limits.Email);
        columns["Phone"].MaxLength.ShouldBe(Limits.Phone);
        columns["Notes"].MaxLength.ShouldBe(Limits.Notes);

        var indexes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        await using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = """
                SELECT name FROM sys.indexes
                WHERE object_id = OBJECT_ID('Leads') AND name IS NOT NULL
                """;

            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                indexes.Add(reader.GetString(0));
            }
        }

        indexes.ShouldContain("UX_Leads_LeadCode");
        indexes.ShouldContain("IX_Leads_OwnerId_Status");
        indexes.ShouldContain("IX_Leads_Status_CreatedDate");
        indexes.ShouldContain("IX_Leads_LeadName");
    }

    [Fact]
    public async Task Contact_check_rejects_both_null()
    {
        var ownerId = await SeedUserAsync();
        await using var connection = new SqlConnection(Db.ConnectionString);
        await connection.OpenAsync();

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = $"""
            INSERT INTO Leads (LeadCode, LeadName, Source, Status, Priority, ExpectedValue, OwnerId, IsDeleted, CreatedDate, CreatedBy)
            VALUES ('LEAD-TEST01', 'Test Lead', 1, 1, 1, 0, '{ownerId}', 0, SYSUTCDATETIME(), 'admin')
            """;

        var ex = await Should.ThrowAsync<SqlException>(() => cmd.ExecuteNonQueryAsync());
        ex.Message.ShouldContain("CK_Leads_Contact");
    }

    [Fact]
    public async Task Expected_value_check_bounds()
    {
        var ownerId = await SeedUserAsync();
        await using var connection = new SqlConnection(Db.ConnectionString);
        await connection.OpenAsync();

        // Negative expected value
        await using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = $"""
                INSERT INTO Leads (LeadCode, LeadName, Email, Source, Status, Priority, ExpectedValue, OwnerId, IsDeleted, CreatedDate, CreatedBy)
                VALUES ('LEAD-TEST02', 'Test Lead', 'test@example.com', 1, 1, 1, -5, '{ownerId}', 0, SYSUTCDATETIME(), 'admin')
                """;

            var ex = await Should.ThrowAsync<SqlException>(() => cmd.ExecuteNonQueryAsync());
            ex.Message.ShouldContain("CK_Leads_ExpectedValue");
        }

        // Expected value > 100,000,000
        await using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = $"""
                INSERT INTO Leads (LeadCode, LeadName, Email, Source, Status, Priority, ExpectedValue, OwnerId, IsDeleted, CreatedDate, CreatedBy)
                VALUES ('LEAD-TEST03', 'Test Lead', 'test@example.com', 1, 1, 1, 100000000.01, '{ownerId}', 0, SYSUTCDATETIME(), 'admin')
                """;

            var ex = await Should.ThrowAsync<SqlException>(() => cmd.ExecuteNonQueryAsync());
            ex.Message.ShouldContain("CK_Leads_ExpectedValue");
        }
    }

    [Fact]
    public async Task Converted_check_ties_status_to_customer_id()
    {
        var ownerId = await SeedUserAsync();
        var customerId = await SeedCustomerAsync(ownerId);
        await using var connection = new SqlConnection(Db.ConnectionString);
        await connection.OpenAsync();

        // Status = Converted (5) but ConvertedCustomerId is NULL -> reject
        await using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = $"""
                INSERT INTO Leads (LeadCode, LeadName, Email, Source, Status, Priority, ExpectedValue, OwnerId, ConvertedCustomerId, IsDeleted, CreatedDate, CreatedBy)
                VALUES ('LEAD-TEST04', 'Test Lead', 'test@example.com', 1, 5, 1, 0, '{ownerId}', NULL, 0, SYSUTCDATETIME(), 'admin')
                """;

            var ex = await Should.ThrowAsync<SqlException>(() => cmd.ExecuteNonQueryAsync());
            ex.Message.ShouldContain("CK_Leads_Converted");
        }

        // Status != Converted (e.g. 1 New) but ConvertedCustomerId is NOT NULL -> reject
        await using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = $"""
                INSERT INTO Leads (LeadCode, LeadName, Email, Source, Status, Priority, ExpectedValue, OwnerId, ConvertedCustomerId, IsDeleted, CreatedDate, CreatedBy)
                VALUES ('LEAD-TEST05', 'Test Lead', 'test@example.com', 1, 1, 1, 0, '{ownerId}', {customerId}, 0, SYSUTCDATETIME(), 'admin')
                """;

            var ex = await Should.ThrowAsync<SqlException>(() => cmd.ExecuteNonQueryAsync());
            ex.Message.ShouldContain("CK_Leads_Converted");
        }
    }

    [Fact]
    public async Task Enums_checks()
    {
        var ownerId = await SeedUserAsync();
        await using var connection = new SqlConnection(Db.ConnectionString);
        await connection.OpenAsync();

        // Invalid Status (e.g. 99)
        await using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = $"""
                INSERT INTO Leads (LeadCode, LeadName, Email, Source, Status, Priority, ExpectedValue, OwnerId, IsDeleted, CreatedDate, CreatedBy)
                VALUES ('LEAD-TEST06', 'Test Lead', 'test@example.com', 1, 99, 1, 0, '{ownerId}', 0, SYSUTCDATETIME(), 'admin')
                """;

            var ex = await Should.ThrowAsync<SqlException>(() => cmd.ExecuteNonQueryAsync());
            ex.Message.ShouldContain("CK_Leads_Status");
        }

        // Invalid Source (e.g. 99)
        await using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = $"""
                INSERT INTO Leads (LeadCode, LeadName, Email, Source, Status, Priority, ExpectedValue, OwnerId, IsDeleted, CreatedDate, CreatedBy)
                VALUES ('LEAD-TEST07', 'Test Lead', 'test@example.com', 99, 1, 1, 0, '{ownerId}', 0, SYSUTCDATETIME(), 'admin')
                """;

            var ex = await Should.ThrowAsync<SqlException>(() => cmd.ExecuteNonQueryAsync());
            ex.Message.ShouldContain("CK_Leads_Source");
        }

        // Invalid Priority (e.g. 99)
        await using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = $"""
                INSERT INTO Leads (LeadCode, LeadName, Email, Source, Status, Priority, ExpectedValue, OwnerId, IsDeleted, CreatedDate, CreatedBy)
                VALUES ('LEAD-TEST08', 'Test Lead', 'test@example.com', 1, 1, 99, 0, '{ownerId}', 0, SYSUTCDATETIME(), 'admin')
                """;

            var ex = await Should.ThrowAsync<SqlException>(() => cmd.ExecuteNonQueryAsync());
            ex.Message.ShouldContain("CK_Leads_Priority");
        }
    }
}

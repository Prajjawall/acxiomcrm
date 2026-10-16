using AcxiomCrm.Domain.Entities;
using AcxiomCrm.Domain.Enums;
using AcxiomCrm.Domain.Validation;
using AcxiomCrm.TestSupport;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Shouldly;

namespace AcxiomCrm.Infrastructure.Tests;

public class CustomerConstraintTests : DbTestBase
{
    public CustomerConstraintTests(SqlServerContainerFixture fixture) : base(fixture)
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
            CreatedDate = new DateTime(2026, 10, 16, 12, 0, 0, DateTimeKind.Utc)
        };
        context.Users.Add(user);
        await context.SaveChangesAsync();
        return user.Id;
    }

    [Fact]
    public async Task Column_lengths_match_Limits_and_index_names_match()
    {
        await using var connection = new SqlConnection(Db.ConnectionString);
        await connection.OpenAsync();

        var columns = new Dictionary<string, (string DataType, int? MaxLength, bool IsNullable)>(StringComparer.OrdinalIgnoreCase);

        await using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = """
                SELECT COLUMN_NAME, DATA_TYPE, CHARACTER_MAXIMUM_LENGTH, IS_NULLABLE
                FROM INFORMATION_SCHEMA.COLUMNS
                WHERE TABLE_NAME = 'Customers'
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

        columns["CustomerId"].DataType.ShouldBe("int");
        columns["CustomerCode"].MaxLength.ShouldBe(20);
        columns["CustomerName"].MaxLength.ShouldBe(Limits.Name);
        columns["CompanyName"].MaxLength.ShouldBe(Limits.Name);
        columns["Email"].MaxLength.ShouldBe(Limits.Email);
        columns["EmailNormalized"].MaxLength.ShouldBe(Limits.Email);
        columns["Phone"].MaxLength.ShouldBe(Limits.Phone);
        columns["Address"].MaxLength.ShouldBe(Limits.Address);
        columns["City"].MaxLength.ShouldBe(Limits.CityState);
        columns["State"].MaxLength.ShouldBe(Limits.CityState);
        columns["Notes"].MaxLength.ShouldBe(Limits.Notes);

        // Check index names
        var indexes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        await using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = """
                SELECT name FROM sys.indexes 
                WHERE object_id = OBJECT_ID('Customers')
                """;
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                if (!reader.IsDBNull(0))
                {
                    indexes.Add(reader.GetString(0));
                }
            }
        }

        indexes.ShouldContain("UX_Customers_EmailNormalized");
        indexes.ShouldContain("UX_Customers_Phone");
        indexes.ShouldContain("IX_Customers_OwnerId_Status");
        indexes.ShouldContain("IX_Customers_CustomerName");
    }

    [Fact]
    public async Task Code_is_generated_as_CUS_000001_and_increments()
    {
        var ownerId = await SeedUserAsync();
        var context = Db.CreateContext();

        var c1 = Customer.Create("Customer One", "Company 1", "c1@example.com", "9876543210", null, null, null, CustomerStatus.Active, ownerId, null);
        var c2 = Customer.Create("Customer Two", "Company 2", "c2@example.com", "9876543211", null, null, null, CustomerStatus.Active, ownerId, null);

        context.Set<Customer>().AddRange(c1, c2);
        await context.SaveChangesAsync();

        c1.Code.ShouldStartWith("CUS-");
        c2.Code.ShouldStartWith("CUS-");
        c1.Code.Length.ShouldBe(10); // CUS-000001
        c2.Code.Length.ShouldBe(10);
    }

    [Fact]
    public async Task Unique_email_among_non_deleted_blocks_duplicate()
    {
        var ownerId = await SeedUserAsync();
        var context = Db.CreateContext();

        var c1 = Customer.Create("Customer A", null, "dup@example.com", "9876543210", null, null, null, CustomerStatus.Active, ownerId, null);
        context.Set<Customer>().Add(c1);
        await context.SaveChangesAsync();

        var context2 = Db.CreateContext();
        var c2 = Customer.Create("Customer B", null, "DUP@example.com", "9876543219", null, null, null, CustomerStatus.Active, ownerId, null);
        context2.Set<Customer>().Add(c2);

        var ex = await Should.ThrowAsync<DbUpdateException>(() => context2.SaveChangesAsync());
        ex.InnerException.ShouldBeOfType<SqlException>();
        ((SqlException)ex.InnerException!).Number.ShouldBeOneOf(2601, 2627);
    }

    [Fact]
    public async Task Unique_email_allows_same_email_when_other_is_soft_deleted()
    {
        var ownerId = await SeedUserAsync();
        var context = Db.CreateContext();

        var c1 = Customer.Create("Customer A", null, "reuse@example.com", "9876543210", null, null, null, CustomerStatus.Active, ownerId, null);
        context.Set<Customer>().Add(c1);
        await context.SaveChangesAsync();

        // Soft delete c1 directly in DB
        await using (var connection = new SqlConnection(Db.ConnectionString))
        {
            await connection.OpenAsync();
            await using var cmd = connection.CreateCommand();
            cmd.CommandText = "UPDATE Customers SET IsDeleted = 1 WHERE CustomerId = @id";
            cmd.Parameters.AddWithValue("@id", c1.Id);
            await cmd.ExecuteNonQueryAsync();
        }

        var context2 = Db.CreateContext();
        var c2 = Customer.Create("Customer B", null, "reuse@example.com", "9876543218", null, null, null, CustomerStatus.Active, ownerId, null);
        context2.Set<Customer>().Add(c2);

        await Should.NotThrowAsync(() => context2.SaveChangesAsync());
    }

    [Fact]
    public async Task Unique_phone_among_non_deleted_blocks_duplicate()
    {
        var ownerId = await SeedUserAsync();
        var context = Db.CreateContext();

        var c1 = Customer.Create("Customer A", null, "p1@example.com", "9876543210", null, null, null, CustomerStatus.Active, ownerId, null);
        context.Set<Customer>().Add(c1);
        await context.SaveChangesAsync();

        var context2 = Db.CreateContext();
        var c2 = Customer.Create("Customer B", null, "p2@example.com", "9876543210", null, null, null, CustomerStatus.Active, ownerId, null);
        context2.Set<Customer>().Add(c2);

        var ex = await Should.ThrowAsync<DbUpdateException>(() => context2.SaveChangesAsync());
        ex.InnerException.ShouldBeOfType<SqlException>();
        ((SqlException)ex.InnerException!).Number.ShouldBeOneOf(2601, 2627);
    }

    [Fact]
    public async Task Unique_phone_allows_same_phone_when_other_is_soft_deleted()
    {
        var ownerId = await SeedUserAsync();
        var context = Db.CreateContext();

        var c1 = Customer.Create("Customer A", null, "p1@example.com", "9876543210", null, null, null, CustomerStatus.Active, ownerId, null);
        context.Set<Customer>().Add(c1);
        await context.SaveChangesAsync();

        // Soft delete c1
        await using (var connection = new SqlConnection(Db.ConnectionString))
        {
            await connection.OpenAsync();
            await using var cmd = connection.CreateCommand();
            cmd.CommandText = "UPDATE Customers SET IsDeleted = 1 WHERE CustomerId = @id";
            cmd.Parameters.AddWithValue("@id", c1.Id);
            await cmd.ExecuteNonQueryAsync();
        }

        var context2 = Db.CreateContext();
        var c2 = Customer.Create("Customer B", null, "p2@example.com", "9876543210", null, null, null, CustomerStatus.Active, ownerId, null);
        context2.Set<Customer>().Add(c2);

        await Should.NotThrowAsync(() => context2.SaveChangesAsync());
    }

    [Fact]
    public async Task Phone_check_rejects_starting_5_or_length_9()
    {
        var ownerId = await SeedUserAsync();
        await using var connection = new SqlConnection(Db.ConnectionString);
        await connection.OpenAsync();

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            INSERT INTO Customers (CustomerName, Email, EmailNormalized, Phone, Status, OwnerId, CreatedDate, CreatedBy, IsDeleted)
            VALUES ('Invalid Phone', 'test@example.com', 'test@example.com', '5876543210', 1, @ownerId, SYSUTCDATETIME(), 'admin', 0)
            """;
        cmd.Parameters.AddWithValue("@ownerId", ownerId);

        var ex = await Should.ThrowAsync<SqlException>(() => cmd.ExecuteNonQueryAsync());
        ex.Number.ShouldBe(547); // Check constraint violation
    }

    [Fact]
    public async Task Status_check_rejects_99()
    {
        var ownerId = await SeedUserAsync();
        await using var connection = new SqlConnection(Db.ConnectionString);
        await connection.OpenAsync();

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            INSERT INTO Customers (CustomerName, Email, EmailNormalized, Phone, Status, OwnerId, CreatedDate, CreatedBy, IsDeleted)
            VALUES ('Invalid Status', 'status@example.com', 'status@example.com', '9876543210', 99, @ownerId, SYSUTCDATETIME(), 'admin', 0)
            """;
        cmd.Parameters.AddWithValue("@ownerId", ownerId);

        var ex = await Should.ThrowAsync<SqlException>(() => cmd.ExecuteNonQueryAsync());
        ex.Number.ShouldBe(547); // Check constraint violation
    }

    [Fact]
    public async Task Owner_fk_restrict()
    {
        await using var connection = new SqlConnection(Db.ConnectionString);
        await connection.OpenAsync();

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            INSERT INTO Customers (CustomerName, Email, EmailNormalized, Phone, Status, OwnerId, CreatedDate, CreatedBy, IsDeleted)
            VALUES ('Invalid Owner', 'owner@example.com', 'owner@example.com', '9876543210', 1, 'non-existent-user-id', SYSUTCDATETIME(), 'admin', 0)
            """;

        var ex = await Should.ThrowAsync<SqlException>(() => cmd.ExecuteNonQueryAsync());
        ex.Number.ShouldBe(547); // FK constraint violation
    }
}

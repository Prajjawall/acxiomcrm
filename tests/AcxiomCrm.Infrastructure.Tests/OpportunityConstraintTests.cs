using AcxiomCrm.Domain.Entities;
using AcxiomCrm.Domain.Enums;
using AcxiomCrm.TestSupport;
using Microsoft.Data.SqlClient;
using Shouldly;

namespace AcxiomCrm.Infrastructure.Tests;

public class OpportunityConstraintTests : DbTestBase
{
    private static readonly DateTime FixedDate = new(2026, 10, 18, 12, 0, 0, DateTimeKind.Utc);
    private static readonly DateOnly Today = new(2026, 10, 18);

    public OpportunityConstraintTests(SqlServerContainerFixture fixture) : base(fixture)
    {
    }

    private async Task<string> SeedUserAsync()
    {
        var context = Db.CreateContext();
        var user = new Identity.ApplicationUser
        {
            Id = Guid.NewGuid().ToString(),
            UserName = "exec" + Guid.NewGuid().ToString("N")[..6],
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
            "Acme Customer",
            null,
            "cust" + Guid.NewGuid().ToString("N")[..6] + "@example.com",
            "9876543210",
            null, null, null,
            CustomerStatus.Active,
            ownerId,
            null);
        context.Customers.Add(customer);
        await context.SaveChangesAsync();
        return customer.Id;
    }

    [Fact]
    public async Task Amount_negative_violates_CK_Opps_Amount()
    {
        var ownerId = await SeedUserAsync();
        var customerId = await SeedCustomerAsync(ownerId);

        using var conn = new SqlConnection(Db.ConnectionString);
        await conn.OpenAsync();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO Opportunities (OpportunityName, CustomerId, Amount, Stage, Status, Probability, ExpectedCloseDate, OwnerId, IsDeleted, CreatedDate, CreatedBy)
            VALUES ('Deal Bad Amount', @cid, -10.00, 1, 1, 50, '2026-11-01', @uid, 0, SYSUTCDATETIME(), @uid);";
        cmd.Parameters.AddWithValue("@cid", customerId);
        cmd.Parameters.AddWithValue("@uid", ownerId);

        var ex = await Should.ThrowAsync<SqlException>(() => cmd.ExecuteNonQueryAsync());
        ex.Message.ShouldContain("CK_Opps_Amount");
    }

    [Fact]
    public async Task Open_opportunity_with_zero_amount_violates_CK_Opps_OpenAmountPositive()
    {
        var ownerId = await SeedUserAsync();
        var customerId = await SeedCustomerAsync(ownerId);

        using var conn = new SqlConnection(Db.ConnectionString);
        await conn.OpenAsync();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO Opportunities (OpportunityName, CustomerId, Amount, Stage, Status, Probability, ExpectedCloseDate, OwnerId, IsDeleted, CreatedDate, CreatedBy)
            VALUES ('Deal Zero Open', @cid, 0.00, 1, 1, 50, '2026-11-01', @uid, 0, SYSUTCDATETIME(), @uid);";
        cmd.Parameters.AddWithValue("@cid", customerId);
        cmd.Parameters.AddWithValue("@uid", ownerId);

        var ex = await Should.ThrowAsync<SqlException>(() => cmd.ExecuteNonQueryAsync());
        ex.Message.ShouldContain("CK_Opps_OpenAmountPositive");
    }

    [Fact]
    public async Task Probability_outside_range_violates_CK_Opps_Probability()
    {
        var ownerId = await SeedUserAsync();
        var customerId = await SeedCustomerAsync(ownerId);

        using var conn = new SqlConnection(Db.ConnectionString);
        await conn.OpenAsync();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO Opportunities (OpportunityName, CustomerId, Amount, Stage, Status, Probability, ExpectedCloseDate, OwnerId, IsDeleted, CreatedDate, CreatedBy)
            VALUES ('Deal Invalid Prob', @cid, 1000.00, 1, 1, 150, '2026-11-01', @uid, 0, SYSUTCDATETIME(), @uid);";
        cmd.Parameters.AddWithValue("@cid", customerId);
        cmd.Parameters.AddWithValue("@uid", ownerId);

        var ex = await Should.ThrowAsync<SqlException>(() => cmd.ExecuteNonQueryAsync());
        ex.Message.ShouldContain("CK_Opps_Probability");
    }

    [Fact]
    public async Task Inconsistent_stage_and_status_violates_CK_Opps_StageStatus()
    {
        var ownerId = await SeedUserAsync();
        var customerId = await SeedCustomerAsync(ownerId);

        using var conn = new SqlConnection(Db.ConnectionString);
        await conn.OpenAsync();
        using var cmd = conn.CreateCommand();
        // Stage 1 (Qualification) cannot have Status 2 (Won)
        cmd.CommandText = @"
            INSERT INTO Opportunities (OpportunityName, CustomerId, Amount, Stage, Status, Probability, ExpectedCloseDate, ClosedDate, OwnerId, IsDeleted, CreatedDate, CreatedBy)
            VALUES ('Deal Mismatched Status', @cid, 1000.00, 1, 2, 50, '2026-11-01', '2026-11-01', @uid, 0, SYSUTCDATETIME(), @uid);";
        cmd.Parameters.AddWithValue("@cid", customerId);
        cmd.Parameters.AddWithValue("@uid", ownerId);

        var ex = await Should.ThrowAsync<SqlException>(() => cmd.ExecuteNonQueryAsync());
        ex.Message.ShouldContain("CK_Opps_StageStatus");
    }

    [Fact]
    public async Task Closed_opportunity_without_closed_date_violates_CK_Opps_ClosedDate()
    {
        var ownerId = await SeedUserAsync();
        var customerId = await SeedCustomerAsync(ownerId);

        using var conn = new SqlConnection(Db.ConnectionString);
        await conn.OpenAsync();
        using var cmd = conn.CreateCommand();
        // Status 2 (Won) must have ClosedDate non-null
        cmd.CommandText = @"
            INSERT INTO Opportunities (OpportunityName, CustomerId, Amount, Stage, Status, Probability, ExpectedCloseDate, ClosedDate, OwnerId, IsDeleted, CreatedDate, CreatedBy)
            VALUES ('Deal Won No Date', @cid, 1000.00, 4, 2, 100, '2026-11-01', NULL, @uid, 0, SYSUTCDATETIME(), @uid);";
        cmd.Parameters.AddWithValue("@cid", customerId);
        cmd.Parameters.AddWithValue("@uid", ownerId);

        var ex = await Should.ThrowAsync<SqlException>(() => cmd.ExecuteNonQueryAsync());
        ex.Message.ShouldContain("CK_Opps_ClosedDate");
    }

    [Fact]
    public async Task Customer_FK_restrict_deletion_when_opportunities_exist()
    {
        var ownerId = await SeedUserAsync();
        var customerId = await SeedCustomerAsync(ownerId);

        var context = Db.CreateContext();
        var opp = Opportunity.Create(
            "Deal Attached",
            customerId,
            25000m,
            OpportunityStage.Qualification,
            20,
            Today.AddDays(20),
            ownerId);
        context.Opportunities.Add(opp);
        await context.SaveChangesAsync();

        using var conn = new SqlConnection(Db.ConnectionString);
        await conn.OpenAsync();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM Customers WHERE CustomerId = @cid;";
        cmd.Parameters.AddWithValue("@cid", customerId);

        var ex = await Should.ThrowAsync<SqlException>(() => cmd.ExecuteNonQueryAsync());
        ex.Message.ShouldContain("FK_");
    }

    [Fact]
    public async Task Index_names_exist()
    {
        using var conn = new SqlConnection(Db.ConnectionString);
        await conn.OpenAsync();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT name FROM sys.indexes 
            WHERE object_id = OBJECT_ID('Opportunities') AND name LIKE 'IX_Opps_%';";

        var indexes = new List<string>();
        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            indexes.Add(reader.GetString(0));
        }

        indexes.ShouldContain("IX_Opps_OwnerId_Status");
        indexes.ShouldContain("IX_Opps_CustomerId");
        indexes.ShouldContain("IX_Opps_Stage_ExpectedCloseDate");
        indexes.ShouldContain("IX_Opps_Status_ClosedDate");
    }
}

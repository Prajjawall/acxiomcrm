using AcxiomCrm.Domain.Entities;
using AcxiomCrm.Domain.Enums;
using AcxiomCrm.TestSupport;
using Microsoft.Data.SqlClient;
using Shouldly;

namespace AcxiomCrm.Infrastructure.Tests;

public class FollowUpConstraintTests : DbTestBase
{
    private static readonly DateTime FixedDate = new(2026, 10, 19, 10, 0, 0, DateTimeKind.Utc);

    public FollowUpConstraintTests(SqlServerContainerFixture fixture) : base(fixture)
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

    private async Task<int> SeedLeadAsync(string ownerId)
    {
        var context = Db.CreateContext();
        var lead = Lead.Create(
            "Prospect Lead",
            "Company",
            "lead" + Guid.NewGuid().ToString("N")[..6] + "@example.com",
            "9876543211",
            LeadSource.Website,
            LeadPriority.Medium,
            50000m,
            ownerId,
            null);
        context.Leads.Add(lead);
        await context.SaveChangesAsync();
        return lead.Id;
    }

    [Fact]
    public async Task Zero_related_records_violates_CK_FollowUps_ExactlyOneRelated()
    {
        var ownerId = await SeedUserAsync();

        using var conn = new SqlConnection(Db.ConnectionString);
        await conn.OpenAsync();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO FollowUps (CustomerId, LeadId, OpportunityId, FollowUpDate, Subject, Type, Status, AssignedToId, RescheduleCount, IsHistoricalEntry, IsDeleted, CreatedDate, CreatedBy)
            VALUES (NULL, NULL, NULL, @FollowUpDate, 'Test Subject', 1, 1, @AssignedToId, 0, 0, 0, @CreatedDate, @AssignedToId);";
        cmd.Parameters.AddWithValue("@FollowUpDate", FixedDate);
        cmd.Parameters.AddWithValue("@AssignedToId", ownerId);
        cmd.Parameters.AddWithValue("@CreatedDate", FixedDate);

        var ex = await Should.ThrowAsync<SqlException>(() => cmd.ExecuteNonQueryAsync());
        ex.Message.ShouldContain("CK_FollowUps_ExactlyOneRelated");
    }

    [Fact]
    public async Task Multiple_related_records_violates_CK_FollowUps_ExactlyOneRelated()
    {
        var ownerId = await SeedUserAsync();
        var customerId = await SeedCustomerAsync(ownerId);
        var leadId = await SeedLeadAsync(ownerId);

        using var conn = new SqlConnection(Db.ConnectionString);
        await conn.OpenAsync();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO FollowUps (CustomerId, LeadId, OpportunityId, FollowUpDate, Subject, Type, Status, AssignedToId, RescheduleCount, IsHistoricalEntry, IsDeleted, CreatedDate, CreatedBy)
            VALUES (@CustomerId, @LeadId, NULL, @FollowUpDate, 'Test Subject', 1, 1, @AssignedToId, 0, 0, 0, @CreatedDate, @AssignedToId);";
        cmd.Parameters.AddWithValue("@CustomerId", customerId);
        cmd.Parameters.AddWithValue("@LeadId", leadId);
        cmd.Parameters.AddWithValue("@FollowUpDate", FixedDate);
        cmd.Parameters.AddWithValue("@AssignedToId", ownerId);
        cmd.Parameters.AddWithValue("@CreatedDate", FixedDate);

        var ex = await Should.ThrowAsync<SqlException>(() => cmd.ExecuteNonQueryAsync());
        ex.Message.ShouldContain("CK_FollowUps_ExactlyOneRelated");
    }

    [Fact]
    public async Task Invalid_status_violates_CK_FollowUps_Status()
    {
        var ownerId = await SeedUserAsync();
        var customerId = await SeedCustomerAsync(ownerId);

        using var conn = new SqlConnection(Db.ConnectionString);
        await conn.OpenAsync();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO FollowUps (CustomerId, LeadId, OpportunityId, FollowUpDate, Subject, Type, Status, AssignedToId, RescheduleCount, IsHistoricalEntry, IsDeleted, CreatedDate, CreatedBy)
            VALUES (@CustomerId, NULL, NULL, @FollowUpDate, 'Test Subject', 1, 99, @AssignedToId, 0, 0, 0, @CreatedDate, @AssignedToId);";
        cmd.Parameters.AddWithValue("@CustomerId", customerId);
        cmd.Parameters.AddWithValue("@FollowUpDate", FixedDate);
        cmd.Parameters.AddWithValue("@AssignedToId", ownerId);
        cmd.Parameters.AddWithValue("@CreatedDate", FixedDate);

        var ex = await Should.ThrowAsync<SqlException>(() => cmd.ExecuteNonQueryAsync());
        ex.Message.ShouldContain("CK_FollowUps_Status");
    }

    [Fact]
    public async Task Invalid_type_violates_CK_FollowUps_Type()
    {
        var ownerId = await SeedUserAsync();
        var customerId = await SeedCustomerAsync(ownerId);

        using var conn = new SqlConnection(Db.ConnectionString);
        await conn.OpenAsync();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO FollowUps (CustomerId, LeadId, OpportunityId, FollowUpDate, Subject, Type, Status, AssignedToId, RescheduleCount, IsHistoricalEntry, IsDeleted, CreatedDate, CreatedBy)
            VALUES (@CustomerId, NULL, NULL, @FollowUpDate, 'Test Subject', 99, 1, @AssignedToId, 0, 0, 0, @CreatedDate, @AssignedToId);";
        cmd.Parameters.AddWithValue("@CustomerId", customerId);
        cmd.Parameters.AddWithValue("@FollowUpDate", FixedDate);
        cmd.Parameters.AddWithValue("@AssignedToId", ownerId);
        cmd.Parameters.AddWithValue("@CreatedDate", FixedDate);

        var ex = await Should.ThrowAsync<SqlException>(() => cmd.ExecuteNonQueryAsync());
        ex.Message.ShouldContain("CK_FollowUps_Type");
    }
}

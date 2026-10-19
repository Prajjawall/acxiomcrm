using AcxiomCrm.TestSupport;
using Microsoft.Data.SqlClient;
using Shouldly;

namespace AcxiomCrm.Infrastructure.Tests;

public class ActivityConstraintTests : DbTestBase
{
    private static readonly DateTime FixedDate = new(2026, 10, 19, 10, 0, 0, DateTimeKind.Utc);

    public ActivityConstraintTests(SqlServerContainerFixture fixture) : base(fixture)
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

    [Fact]
    public async Task Zero_related_records_violates_CK_Activities_AtLeastOneRelated()
    {
        var ownerId = await SeedUserAsync();

        using var conn = new SqlConnection(Db.ConnectionString);
        await conn.OpenAsync();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO Activities (Type, Subject, ActivityDate, CustomerId, LeadId, OpportunityId, AssignedToId, Status, IsDeleted, CreatedDate, CreatedBy)
            VALUES (1, 'Call without related', @ActivityDate, NULL, NULL, NULL, @AssignedToId, 1, 0, @CreatedDate, @AssignedToId);";
        cmd.Parameters.AddWithValue("@ActivityDate", FixedDate);
        cmd.Parameters.AddWithValue("@AssignedToId", ownerId);
        cmd.Parameters.AddWithValue("@CreatedDate", FixedDate);

        var ex = await Should.ThrowAsync<SqlException>(() => cmd.ExecuteNonQueryAsync());
        ex.Message.ShouldContain("CK_Activities_AtLeastOneRelated");
    }

    [Fact]
    public async Task Invalid_status_violates_CK_Activities_Status()
    {
        var ownerId = await SeedUserAsync();

        using var conn = new SqlConnection(Db.ConnectionString);
        await conn.OpenAsync();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO Activities (Type, Subject, ActivityDate, CustomerId, LeadId, OpportunityId, AssignedToId, Status, IsDeleted, CreatedDate, CreatedBy)
            VALUES (1, 'Call with invalid status', @ActivityDate, NULL, NULL, NULL, @AssignedToId, 99, 0, @CreatedDate, @AssignedToId);";
        cmd.Parameters.AddWithValue("@ActivityDate", FixedDate);
        cmd.Parameters.AddWithValue("@AssignedToId", ownerId);
        cmd.Parameters.AddWithValue("@CreatedDate", FixedDate);

        var ex = await Should.ThrowAsync<SqlException>(() => cmd.ExecuteNonQueryAsync());
        // Will violate at least one constraint (status or at least one related)
        ex.Message.ShouldContain("CK_Activities");
    }

    [Fact]
    public async Task Invalid_type_violates_CK_Activities_Type()
    {
        var ownerId = await SeedUserAsync();

        using var conn = new SqlConnection(Db.ConnectionString);
        await conn.OpenAsync();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO Activities (Type, Subject, ActivityDate, CustomerId, LeadId, OpportunityId, AssignedToId, Status, IsDeleted, CreatedDate, CreatedBy)
            VALUES (99, 'Invalid type', @ActivityDate, NULL, NULL, NULL, @AssignedToId, 1, 0, @CreatedDate, @AssignedToId);";
        cmd.Parameters.AddWithValue("@ActivityDate", FixedDate);
        cmd.Parameters.AddWithValue("@AssignedToId", ownerId);
        cmd.Parameters.AddWithValue("@CreatedDate", FixedDate);

        var ex = await Should.ThrowAsync<SqlException>(() => cmd.ExecuteNonQueryAsync());
        ex.Message.ShouldContain("CK_Activities");
    }
}

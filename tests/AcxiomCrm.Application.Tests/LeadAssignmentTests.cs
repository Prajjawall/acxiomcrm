using System.Globalization;
using AcxiomCrm.Application.Common;
using AcxiomCrm.Application.Leads;
using AcxiomCrm.Domain.Audit;
using AcxiomCrm.Domain.Enums;
using AcxiomCrm.Domain.Security;
using AcxiomCrm.TestSupport;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace AcxiomCrm.Application.Tests;

public class LeadAssignmentTests : DbTestBase
{
    public LeadAssignmentTests(SqlServerContainerFixture fixture) : base(fixture)
    {
    }

    [Fact]
    public async Task Executive_assign_is_Forbidden()
    {
        await using var ctx = Db.CreateContext();
        var users = await TestUsers.CreateStandardAsync(ctx);

        using var harness = ServiceHarness.Create(Db, users.ExecN1Id, Roles.SalesExecutive);
        var service = harness.Services.GetRequiredService<ILeadService>();

        var created = await service.CreateAsync(new CreateLeadCommand(
            "Lead One", null, "l1@example.com", "9876543210",
            LeadSource.Website, LeadPriority.Low, 1000m, null, null));
        created.Succeeded.ShouldBeTrue();

        var assign = await service.AssignAsync(created.Value!.Id, users.ExecN2Id, created.Value.RowVersion);
        assign.Succeeded.ShouldBeFalse();
        assign.Kind.ShouldBe(ErrorKind.Forbidden);
    }

    [Fact]
    public async Task Manager_assigns_within_team_succeeds_and_writes_audit()
    {
        await using var ctx = Db.CreateContext();
        var users = await TestUsers.CreateStandardAsync(ctx);

        // Activate ExecN2 for this test
        var execN2 = await ctx.Users.FirstAsync(u => u.Id == users.ExecN2Id);
        execN2.IsActive = true;
        await ctx.SaveChangesAsync();

        // Manager creates lead assigned to ExecN1
        int leadId;
        byte[] rowVersion;
        using (var mgrHarness = ServiceHarness.Create(Db, users.NorthManagerId, Roles.Manager))
        {
            var service = mgrHarness.Services.GetRequiredService<ILeadService>();
            var created = await service.CreateAsync(new CreateLeadCommand(
                "Team Lead", null, "team@example.com", "9876543210",
                LeadSource.Website, LeadPriority.Medium, 5000m, users.ExecN1Id, null));
            created.Succeeded.ShouldBeTrue();
            leadId = created.Value!.Id;
            rowVersion = created.Value.RowVersion;

            // Manager reassigns to ExecN2 (same North team)
            var assign = await service.AssignAsync(leadId, users.ExecN2Id, rowVersion);
            assign.Succeeded.ShouldBeTrue();
            assign.Value!.OwnerId.ShouldBe(users.ExecN2Id);
        }

        // Verify audit log
        await using var verifyCtx = Db.CreateContext();
        var audit = await verifyCtx.AuditLogs
            .Where(a => a.Module == AuditModules.Leads && a.Action == AuditActions.Assigned && a.RecordId == leadId.ToString(CultureInfo.InvariantCulture))
            .OrderByDescending(a => a.AuditLogId)
            .FirstOrDefaultAsync();

        audit.ShouldNotBeNull();
        audit.UserId.ShouldBe(users.NorthManagerId);
        audit.OldValue.ShouldNotBeNull();
        audit.NewValue.ShouldNotBeNull();
        audit.OldValue.ShouldContain(users.ExecN1Id);
        audit.NewValue.ShouldContain(users.ExecN2Id);
    }

    [Fact]
    public async Task Manager_assigns_outside_team_rejected()
    {
        await using var ctx = Db.CreateContext();
        var users = await TestUsers.CreateStandardAsync(ctx);

        using var mgrHarness = ServiceHarness.Create(Db, users.NorthManagerId, Roles.Manager);
        var service = mgrHarness.Services.GetRequiredService<ILeadService>();

        var created = await service.CreateAsync(new CreateLeadCommand(
            "North Lead", null, "north@example.com", "9876543210",
            LeadSource.Website, LeadPriority.Medium, 5000m, users.ExecN1Id, null));
        created.Succeeded.ShouldBeTrue();

        // North Manager tries to assign to South Executive (outside team)
        var assign = await service.AssignAsync(created.Value!.Id, users.ExecS1Id, created.Value.RowVersion);
        assign.Succeeded.ShouldBeFalse();
        assign.Kind.ShouldBe(ErrorKind.Validation);
        assign.Errors.ShouldContain(e => e.Field == "OwnerId");
    }

    [Fact]
    public async Task Deactivated_target_rejected()
    {
        await using var ctx = Db.CreateContext();
        var users = await TestUsers.CreateStandardAsync(ctx);

        // Deactivate ExecN2
        var deactivatedUser = await ctx.Users.FirstAsync(u => u.Id == users.ExecN2Id);
        deactivatedUser.IsActive = false;
        await ctx.SaveChangesAsync();

        using var mgrHarness = ServiceHarness.Create(Db, users.NorthManagerId, Roles.Manager);
        var service = mgrHarness.Services.GetRequiredService<ILeadService>();

        var created = await service.CreateAsync(new CreateLeadCommand(
            "North Lead 2", null, "north2@example.com", "9876543210",
            LeadSource.Website, LeadPriority.Medium, 5000m, users.ExecN1Id, null));
        created.Succeeded.ShouldBeTrue();

        // Assign to deactivated user
        var assign = await service.AssignAsync(created.Value!.Id, users.ExecN2Id, created.Value.RowVersion);
        assign.Succeeded.ShouldBeFalse();
        assign.Kind.ShouldBe(ErrorKind.Validation);
        assign.Errors.ShouldContain(e => e.Field == "OwnerId");
    }
}

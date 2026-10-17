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

public class LeadStatusTests : DbTestBase
{
    public LeadStatusTests(SqlServerContainerFixture fixture) : base(fixture)
    {
    }

    [Theory]
    [InlineData(LeadStatus.New, LeadStatus.Contacted)]
    [InlineData(LeadStatus.New, LeadStatus.Unqualified)]
    [InlineData(LeadStatus.New, LeadStatus.Lost)]
    [InlineData(LeadStatus.Contacted, LeadStatus.Qualified)]
    [InlineData(LeadStatus.Contacted, LeadStatus.Unqualified)]
    [InlineData(LeadStatus.Contacted, LeadStatus.Lost)]
    [InlineData(LeadStatus.Qualified, LeadStatus.Contacted)]
    [InlineData(LeadStatus.Qualified, LeadStatus.Unqualified)]
    [InlineData(LeadStatus.Qualified, LeadStatus.Lost)]
    [InlineData(LeadStatus.Unqualified, LeadStatus.Contacted)]
    [InlineData(LeadStatus.Unqualified, LeadStatus.Lost)]
    public async Task Legal_transitions_succeed_and_write_StatusChanged_audit(LeadStatus from, LeadStatus to)
    {
        await using var ctx = Db.CreateContext();
        var users = await TestUsers.CreateStandardAsync(ctx);

        using var harness = ServiceHarness.Create(Db, users.AdminId, Roles.Admin);
        var service = harness.Services.GetRequiredService<ILeadService>();

        // Create initial lead in New
        var created = await service.CreateAsync(new CreateLeadCommand(
            "Status Test Lead", null, "status@example.com", "9876543210",
            LeadSource.Website, LeadPriority.Medium, 1000m, null, null));
        created.Succeeded.ShouldBeTrue();
        var leadId = created.Value!.Id;
        var rowVersion = created.Value.RowVersion;

        // Move to 'from' status if not already New
        if (from != LeadStatus.New)
        {
            if (from is LeadStatus.Contacted)
            {
                var s = await service.ChangeStatusAsync(leadId, LeadStatus.Contacted, rowVersion);
                rowVersion = s.Value!.RowVersion;
            }
            else if (from is LeadStatus.Qualified)
            {
                var s1 = await service.ChangeStatusAsync(leadId, LeadStatus.Contacted, rowVersion);
                var s2 = await service.ChangeStatusAsync(leadId, LeadStatus.Qualified, s1.Value!.RowVersion);
                rowVersion = s2.Value!.RowVersion;
            }
            else if (from is LeadStatus.Unqualified)
            {
                var s = await service.ChangeStatusAsync(leadId, LeadStatus.Unqualified, rowVersion);
                rowVersion = s.Value!.RowVersion;
            }
            else if (from is LeadStatus.Lost)
            {
                var s = await service.ChangeStatusAsync(leadId, LeadStatus.Lost, rowVersion);
                rowVersion = s.Value!.RowVersion;
            }
        }

        // Now perform the transition under test: from -> to
        var result = await service.ChangeStatusAsync(leadId, to, rowVersion);
        result.Succeeded.ShouldBeTrue();
        result.Value!.Status.ShouldBe(to);

        // Verify StatusChanged audit log was recorded
        await using var verifyCtx = Db.CreateContext();
        var audit = await verifyCtx.AuditLogs
            .Where(a => a.Module == AuditModules.Leads && a.Action == AuditActions.StatusChanged && a.RecordId == leadId.ToString(CultureInfo.InvariantCulture))
            .OrderByDescending(a => a.AuditLogId)
            .FirstOrDefaultAsync();

        audit.ShouldNotBeNull();
        audit.OldValue.ShouldNotBeNull();
        audit.NewValue.ShouldNotBeNull();
        audit.OldValue.ShouldContain(from.ToString());
        audit.NewValue.ShouldContain(to.ToString());
    }

    [Theory]
    [InlineData(LeadStatus.New, LeadStatus.Qualified)]
    [InlineData(LeadStatus.Contacted, LeadStatus.New)]
    [InlineData(LeadStatus.Qualified, LeadStatus.New)]
    [InlineData(LeadStatus.Unqualified, LeadStatus.New)]
    [InlineData(LeadStatus.Unqualified, LeadStatus.Qualified)]
    [InlineData(LeadStatus.Lost, LeadStatus.Contacted)]
    [InlineData(LeadStatus.Lost, LeadStatus.Qualified)]
    [InlineData(LeadStatus.Lost, LeadStatus.Unqualified)]
    public async Task Illegal_transition_returns_conflict_with_message(LeadStatus from, LeadStatus to)
    {
        await using var ctx = Db.CreateContext();
        var users = await TestUsers.CreateStandardAsync(ctx);

        using var harness = ServiceHarness.Create(Db, users.AdminId, Roles.Admin);
        var service = harness.Services.GetRequiredService<ILeadService>();

        var created = await service.CreateAsync(new CreateLeadCommand(
            "Illegal Move Lead", null, "illegal@example.com", "9876543210",
            LeadSource.Website, LeadPriority.Medium, 1000m, null, null));
        created.Succeeded.ShouldBeTrue();
        var leadId = created.Value!.Id;
        var rowVersion = created.Value.RowVersion;

        // Reach 'from' status
        if (from == LeadStatus.Contacted)
        {
            var s = await service.ChangeStatusAsync(leadId, LeadStatus.Contacted, rowVersion);
            rowVersion = s.Value!.RowVersion;
        }
        else if (from == LeadStatus.Qualified)
        {
            var s1 = await service.ChangeStatusAsync(leadId, LeadStatus.Contacted, rowVersion);
            var s2 = await service.ChangeStatusAsync(leadId, LeadStatus.Qualified, s1.Value!.RowVersion);
            rowVersion = s2.Value!.RowVersion;
        }
        else if (from == LeadStatus.Unqualified)
        {
            var s = await service.ChangeStatusAsync(leadId, LeadStatus.Unqualified, rowVersion);
            rowVersion = s.Value!.RowVersion;
        }
        else if (from == LeadStatus.Lost)
        {
            var s = await service.ChangeStatusAsync(leadId, LeadStatus.Lost, rowVersion);
            rowVersion = s.Value!.RowVersion;
        }

        var result = await service.ChangeStatusAsync(leadId, to, rowVersion);
        result.Succeeded.ShouldBeFalse();
        result.Kind.ShouldBe(ErrorKind.Conflict);
        result.Errors[0].Message.ShouldBe($"A lead cannot move from {from} to {to}.");
    }

    [Fact]
    public async Task Converted_via_ChangeStatus_is_rejected()
    {
        await using var ctx = Db.CreateContext();
        var users = await TestUsers.CreateStandardAsync(ctx);

        using var harness = ServiceHarness.Create(Db, users.AdminId, Roles.Admin);
        var service = harness.Services.GetRequiredService<ILeadService>();

        var created = await service.CreateAsync(new CreateLeadCommand(
            "To Convert", null, "toconvert@example.com", "9876543210",
            LeadSource.Website, LeadPriority.Medium, 1000m, null, null));
        created.Succeeded.ShouldBeTrue();
        var leadId = created.Value!.Id;

        // Move to Qualified
        var s1 = await service.ChangeStatusAsync(leadId, LeadStatus.Contacted, created.Value.RowVersion);
        var s2 = await service.ChangeStatusAsync(leadId, LeadStatus.Qualified, s1.Value!.RowVersion);

        // Try moving to Converted directly via ChangeStatus
        var result = await service.ChangeStatusAsync(leadId, LeadStatus.Converted, s2.Value!.RowVersion);
        result.Succeeded.ShouldBeFalse();
        result.Kind.ShouldBe(ErrorKind.Conflict);
    }

    [Fact]
    public async Task Lost_to_New_by_executive_Forbidden()
    {
        await using var ctx = Db.CreateContext();
        var users = await TestUsers.CreateStandardAsync(ctx);

        // Executive creates and sets to Lost
        int leadId;
        byte[] rowVersion;
        using (var execHarness = ServiceHarness.Create(Db, users.ExecN1Id, Roles.SalesExecutive))
        {
            var execService = execHarness.Services.GetRequiredService<ILeadService>();
            var created = await execService.CreateAsync(new CreateLeadCommand(
                "Executive Lead", null, "execlead@example.com", "9876543210",
                LeadSource.Website, LeadPriority.Medium, 1000m, null, null));
            created.Succeeded.ShouldBeTrue();
            leadId = created.Value!.Id;

            var lost = await execService.ChangeStatusAsync(leadId, LeadStatus.Lost, created.Value.RowVersion);
            lost.Succeeded.ShouldBeTrue();
            rowVersion = lost.Value!.RowVersion;

            // Executive tries to reopen Lost -> New
            var reopen = await execService.ChangeStatusAsync(leadId, LeadStatus.New, rowVersion);
            reopen.Succeeded.ShouldBeFalse();
            reopen.Kind.ShouldBe(ErrorKind.Forbidden);
        }
    }

    [Fact]
    public async Task Lost_to_New_by_manager_allowed_and_audited()
    {
        await using var ctx = Db.CreateContext();
        var users = await TestUsers.CreateStandardAsync(ctx);

        // Executive creates and moves to Lost
        int leadId;
        byte[] rowVersion;
        using (var execHarness = ServiceHarness.Create(Db, users.ExecN1Id, Roles.SalesExecutive))
        {
            var execService = execHarness.Services.GetRequiredService<ILeadService>();
            var created = await execService.CreateAsync(new CreateLeadCommand(
                "Exec Team Lead", null, "teamlead@example.com", "9876543210",
                LeadSource.Website, LeadPriority.Medium, 1000m, null, null));
            leadId = created.Value!.Id;

            var lost = await execService.ChangeStatusAsync(leadId, LeadStatus.Lost, created.Value.RowVersion);
            rowVersion = lost.Value!.RowVersion;
        }

        // Manager of executive (North Manager) reopens to New
        using (var mgrHarness = ServiceHarness.Create(Db, users.NorthManagerId, Roles.Manager))
        {
            var mgrService = mgrHarness.Services.GetRequiredService<ILeadService>();
            var reopen = await mgrService.ChangeStatusAsync(leadId, LeadStatus.New, rowVersion);
            reopen.Succeeded.ShouldBeTrue();
            reopen.Value!.Status.ShouldBe(LeadStatus.New);

            // Verify audit row
            await using var verifyCtx = Db.CreateContext();
            var audit = await verifyCtx.AuditLogs
                .Where(a => a.Module == AuditModules.Leads && a.Action == AuditActions.StatusChanged && a.RecordId == leadId.ToString(CultureInfo.InvariantCulture))
                .OrderByDescending(a => a.AuditLogId)
                .FirstOrDefaultAsync();

            audit.ShouldNotBeNull();
            audit.UserId.ShouldBe(users.NorthManagerId);
            audit.OldValue.ShouldNotBeNull();
            audit.NewValue.ShouldNotBeNull();
            audit.OldValue.ShouldContain("Lost");
            audit.NewValue.ShouldContain("New");
        }
    }
}

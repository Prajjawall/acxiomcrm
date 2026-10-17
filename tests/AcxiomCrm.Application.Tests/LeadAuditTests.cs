using System.Globalization;
using AcxiomCrm.Application.Leads;
using AcxiomCrm.Domain.Audit;
using AcxiomCrm.Domain.Enums;
using AcxiomCrm.Domain.Security;
using AcxiomCrm.TestSupport;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace AcxiomCrm.Application.Tests;

public class LeadAuditTests : DbTestBase
{
    public LeadAuditTests(SqlServerContainerFixture fixture) : base(fixture)
    {
    }

    [Fact]
    public async Task Create_update_delete_produce_audit_rows_with_acting_user()
    {
        await using var ctx = Db.CreateContext();
        var users = await TestUsers.CreateStandardAsync(ctx);

        using var harness = ServiceHarness.Create(Db, users.AdminId, Roles.Admin);
        var service = harness.Services.GetRequiredService<ILeadService>();

        // 1. Create
        var createResult = await service.CreateAsync(new CreateLeadCommand(
            "Audit Lead", null, "lead.audit@example.com", "9876543210",
            LeadSource.Website, LeadPriority.Medium, 1000m, null, null));
        createResult.Succeeded.ShouldBeTrue();
        var leadId = createResult.Value!.Id;

        await using (var verifyCtx = Db.CreateContext())
        {
            var createLog = await verifyCtx.AuditLogs
                .FirstOrDefaultAsync(l => l.Module == AuditModules.Leads && l.Action == AuditActions.Create && l.RecordId == leadId.ToString(CultureInfo.InvariantCulture));

            createLog.ShouldNotBeNull();
            createLog.UserId.ShouldBe(users.AdminId);
            createLog.Result.ShouldBe(AuditResult.Success);
            createLog.NewValue.ShouldNotBeNull();
            createLog.NewValue.ShouldContain("Audit Lead");
        }

        // 2. Update
        var updateResult = await service.UpdateAsync(leadId, new UpdateLeadCommand(
            "Audit Lead Updated", null, "lead.audit@example.com", "9876543210",
            LeadSource.Website, LeadPriority.High, 2000m, "Updated Notes", createResult.Value.RowVersion));
        updateResult.Succeeded.ShouldBeTrue();

        await using (var verifyCtx = Db.CreateContext())
        {
            var updateLog = await verifyCtx.AuditLogs
                .FirstOrDefaultAsync(l => l.Module == AuditModules.Leads && l.Action == AuditActions.Update && l.RecordId == leadId.ToString(CultureInfo.InvariantCulture));

            updateLog.ShouldNotBeNull();
            updateLog.UserId.ShouldBe(users.AdminId);
            updateLog.OldValue.ShouldNotBeNull();
            updateLog.NewValue.ShouldNotBeNull();
            updateLog.OldValue.ShouldContain("Audit Lead");
            updateLog.NewValue.ShouldContain("Audit Lead Updated");
        }

        // 3. Delete
        var deleteResult = await service.DeleteAsync(leadId);
        deleteResult.Succeeded.ShouldBeTrue();

        await using (var verifyCtx = Db.CreateContext())
        {
            var deleteLog = await verifyCtx.AuditLogs
                .FirstOrDefaultAsync(l => l.Module == AuditModules.Leads && l.Action == AuditActions.Delete && l.RecordId == leadId.ToString(CultureInfo.InvariantCulture));

            deleteLog.ShouldNotBeNull();
            deleteLog.UserId.ShouldBe(users.AdminId);
            deleteLog.Result.ShouldBe(AuditResult.Success);
        }
    }
}

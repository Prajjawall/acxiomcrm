using System.Globalization;
using AcxiomCrm.Application.Opportunities;
using AcxiomCrm.Domain.Audit;
using AcxiomCrm.Domain.Entities;
using AcxiomCrm.Domain.Enums;
using AcxiomCrm.Domain.Security;
using AcxiomCrm.TestSupport;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace AcxiomCrm.Application.Tests;

public class OpportunityAuditTests : DbTestBase
{
    public OpportunityAuditTests(SqlServerContainerFixture fixture) : base(fixture)
    {
    }

    [Fact]
    public async Task Create_update_delete_produce_audit_rows_with_acting_user()
    {
        await using var ctx = Db.CreateContext();
        var users = await TestUsers.CreateStandardAsync(ctx);

        var customer = Customer.Create("Audit Cust", null, "auditcust@example.com", "9876543210", null, null, null, CustomerStatus.Active, users.AdminId, null);
        ctx.Customers.Add(customer);
        await ctx.SaveChangesAsync();

        using var harness = ServiceHarness.Create(Db, users.AdminId, Roles.Admin);
        var service = harness.Services.GetRequiredService<IOpportunityService>();

        // 1. Create
        var createResult = await service.CreateAsync(new CreateOpportunityCommand(
            "Audit Deal", customer.Id, 50000m, 20, new DateOnly(2026, 12, 1),
            OpportunityStage.Qualification, null, users.AdminId, "Initial notes"));
        createResult.Succeeded.ShouldBeTrue();
        var oppId = createResult.Value!.Id;

        await using (var verifyCtx = Db.CreateContext())
        {
            var createLog = await verifyCtx.AuditLogs
                .FirstOrDefaultAsync(l => l.Module == AuditModules.Opportunities && l.Action == AuditActions.Create && l.RecordId == oppId.ToString(CultureInfo.InvariantCulture));

            createLog.ShouldNotBeNull();
            createLog.UserId.ShouldBe(users.AdminId);
            createLog.Result.ShouldBe(AuditResult.Success);
            createLog.NewValue.ShouldNotBeNull();
            createLog.NewValue.ShouldContain("Audit Deal");
        }

        // 2. Update
        var updateResult = await service.UpdateAsync(oppId, new UpdateOpportunityCommand(
            "Audit Deal Updated", customer.Id, 75000m, 50, new DateOnly(2026, 12, 1),
            null, users.AdminId, "Updated notes", createResult.Value.RowVersion));
        updateResult.Succeeded.ShouldBeTrue();

        await using (var verifyCtx = Db.CreateContext())
        {
            var updateLog = await verifyCtx.AuditLogs
                .FirstOrDefaultAsync(l => l.Module == AuditModules.Opportunities && l.Action == AuditActions.Update && l.RecordId == oppId.ToString(CultureInfo.InvariantCulture));

            updateLog.ShouldNotBeNull();
            updateLog.UserId.ShouldBe(users.AdminId);
            updateLog.OldValue.ShouldNotBeNull();
            updateLog.NewValue.ShouldNotBeNull();
            updateLog.OldValue.ShouldContain("Audit Deal");
            updateLog.NewValue.ShouldContain("Audit Deal Updated");
        }

        // 3. Delete
        var deleteResult = await service.DeleteAsync(oppId);
        deleteResult.Succeeded.ShouldBeTrue();

        await using (var verifyCtx = Db.CreateContext())
        {
            var deleteLog = await verifyCtx.AuditLogs
                .FirstOrDefaultAsync(l => l.Module == AuditModules.Opportunities && l.Action == AuditActions.Delete && l.RecordId == oppId.ToString(CultureInfo.InvariantCulture));

            deleteLog.ShouldNotBeNull();
            deleteLog.UserId.ShouldBe(users.AdminId);
            deleteLog.Result.ShouldBe(AuditResult.Success);
        }
    }
}

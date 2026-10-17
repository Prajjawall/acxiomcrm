using System.Globalization;
using AcxiomCrm.Application.Customers;
using AcxiomCrm.Domain.Audit;
using AcxiomCrm.Domain.Enums;
using AcxiomCrm.Domain.Security;
using AcxiomCrm.TestSupport;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace AcxiomCrm.Application.Tests;

public class CustomerAuditTests : DbTestBase
{
    public CustomerAuditTests(SqlServerContainerFixture fixture) : base(fixture)
    {
    }

    [Fact]
    public async Task Create_update_delete_produce_audit_rows_with_acting_user()
    {
        await using var ctx = Db.CreateContext();
        var users = await TestUsers.CreateStandardAsync(ctx);

        using var harness = ServiceHarness.Create(Db, users.AdminId, Roles.Admin);
        var service = harness.Services.GetRequiredService<ICustomerService>();

        // 1. Create
        var createResult = await service.CreateAsync(new CreateCustomerCommand(
            "Audit Customer", null, "audit@example.com", "9876543210", null, null, null, CustomerStatus.Active, null, null));
        createResult.Succeeded.ShouldBeTrue();
        var customerId = createResult.Value!.Id;

        await using (var verifyCtx = Db.CreateContext())
        {
            var createLog = await verifyCtx.AuditLogs
                .FirstOrDefaultAsync(l => l.Module == AuditModules.Customers && l.Action == AuditActions.Create && l.RecordId == customerId.ToString(CultureInfo.InvariantCulture));

            createLog.ShouldNotBeNull();
            createLog.UserId.ShouldBe(users.AdminId);
            createLog.Result.ShouldBe(AuditResult.Success);
            createLog.NewValue.ShouldNotBeNull();
            createLog.NewValue.ShouldContain("Audit Customer");
        }

        // 2. Update status from Active to Inactive
        var updateResult = await service.UpdateAsync(customerId, new UpdateCustomerCommand(
            "Audit Customer Updated", null, "audit@example.com", "9876543210", null, null, null, CustomerStatus.Inactive, null, null, createResult.Value.RowVersion));
        updateResult.Succeeded.ShouldBeTrue();

        await using (var verifyCtx = Db.CreateContext())
        {
            var updateLog = await verifyCtx.AuditLogs
                .FirstOrDefaultAsync(l => l.Module == AuditModules.Customers && l.Action == AuditActions.Update && l.RecordId == customerId.ToString(CultureInfo.InvariantCulture));

            updateLog.ShouldNotBeNull();
            updateLog.UserId.ShouldBe(users.AdminId);
            updateLog.OldValue.ShouldNotBeNull();
            updateLog.NewValue.ShouldNotBeNull();
            updateLog.OldValue.ShouldContain("\"Status\":\"Active\"");
            updateLog.NewValue.ShouldContain("\"Status\":\"Inactive\"");
        }

        // 3. Delete
        var delResult = await service.DeleteAsync(customerId);
        delResult.Succeeded.ShouldBeTrue();

        await using (var verifyCtx = Db.CreateContext())
        {
            var deleteLog = await verifyCtx.AuditLogs
                .FirstOrDefaultAsync(l => l.Module == AuditModules.Customers && l.Action == AuditActions.Delete && l.RecordId == customerId.ToString(CultureInfo.InvariantCulture));

            deleteLog.ShouldNotBeNull();
            deleteLog.UserId.ShouldBe(users.AdminId);
            deleteLog.Result.ShouldBe(AuditResult.Success);
        }
    }

    [Fact]
    public async Task Failed_validation_does_not_produce_audit_row()
    {
        await using var ctx = Db.CreateContext();
        var users = await TestUsers.CreateStandardAsync(ctx);

        using var harness = ServiceHarness.Create(Db, users.ExecN1Id, Roles.SalesExecutive);
        var service = harness.Services.GetRequiredService<ICustomerService>();

        var beforeCount = await ctx.AuditLogs.CountAsync();

        var res = await service.CreateAsync(new CreateCustomerCommand(
            "", null, "bad-email", "123", null, null, null, CustomerStatus.Active, null, null));
        res.Succeeded.ShouldBeFalse();

        await using var verifyCtx = Db.CreateContext();
        var afterCount = await verifyCtx.AuditLogs.CountAsync();
        afterCount.ShouldBe(beforeCount);
    }
}

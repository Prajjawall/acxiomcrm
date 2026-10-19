using AcxiomCrm.Application.Users;
using AcxiomCrm.Domain.Security;
using AcxiomCrm.TestSupport;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace AcxiomCrm.Application.Tests;

public class UserAdminAuditTests : DbTestBase
{
    private static readonly DateTime FixedDate = new(2026, 10, 19, 10, 0, 0, DateTimeKind.Utc);

    public UserAdminAuditTests(SqlServerContainerFixture fixture) : base(fixture)
    {
    }

    [Fact]
    public async Task All_mutations_write_expected_audits_without_passwords()
    {
        await using var ctx = Db.CreateContext();
        var users = await TestUsers.CreateStandardAsync(ctx);

        var clock = new FakeClock(FixedDate);
        using var harness = ServiceHarness.Create(Db, users.AdminId, Roles.Admin, clock);
        var service = harness.Services.GetRequiredService<IUserAdminService>();

        // 1. Create
        var createRes = await service.CreateAsync(new CreateUserCommand("Audit Test", "audit.test", "audit.test@acxiom.com", Roles.SalesExecutive, users.NorthManagerId, true));
        createRes.Succeeded.ShouldBeTrue();
        var userId = createRes.Value!.UserId;

        var createAudit = await ctx.AuditLogs.FirstOrDefaultAsync(a => a.RecordId == userId && a.Action == "Create");
        createAudit.ShouldNotBeNull();
        createAudit.Details?.Contains(createRes.Value.TemporaryPassword).ShouldBeFalse();

        // 2. Update
        var updateRes = await service.UpdateAsync(userId, new UpdateUserCommand("Audit Test Updated", "audit.test", "audit.test@acxiom.com", Roles.SalesExecutive, users.NorthManagerId));
        updateRes.Succeeded.ShouldBeTrue();
        var updateAudit = await ctx.AuditLogs.FirstOrDefaultAsync(a => a.RecordId == userId && a.Action == "Update");
        updateAudit.ShouldNotBeNull();

        // 3. RoleChanged
        var roleRes = await service.UpdateAsync(userId, new UpdateUserCommand("Audit Test Updated", "audit.test", "audit.test@acxiom.com", Roles.Manager, null));
        roleRes.Succeeded.ShouldBeTrue();
        var roleAudit = await ctx.AuditLogs.FirstOrDefaultAsync(a => a.RecordId == userId && a.Action == "RoleChanged");
        roleAudit.ShouldNotBeNull();
        roleAudit.OldValue.ShouldBe(Roles.SalesExecutive);
        roleAudit.NewValue.ShouldBe(Roles.Manager);

        // 4. Deactivated
        var deactRes = await service.DeactivateAsync(userId, new DeactivateUserCommand());
        deactRes.Succeeded.ShouldBeTrue();
        var deactAudit = await ctx.AuditLogs.FirstOrDefaultAsync(a => a.RecordId == userId && a.Action == "Deactivated");
        deactAudit.ShouldNotBeNull();

        // 5. Activated
        var actRes = await service.ActivateAsync(userId);
        actRes.Succeeded.ShouldBeTrue();
        var actAudit = await ctx.AuditLogs.FirstOrDefaultAsync(a => a.RecordId == userId && a.Action == "Activated");
        actAudit.ShouldNotBeNull();

        // 6. Unlock
        var unlockRes = await service.UnlockAsync(userId);
        unlockRes.Succeeded.ShouldBeTrue();
        var unlockAudit = await ctx.AuditLogs.FirstOrDefaultAsync(a => a.RecordId == userId && a.Action == "Unlock");
        unlockAudit.ShouldNotBeNull();

        // 7. PasswordReset
        var resetRes = await service.ResetPasswordAsync(userId);
        resetRes.Succeeded.ShouldBeTrue();
        var resetAudit = await ctx.AuditLogs.FirstOrDefaultAsync(a => a.RecordId == userId && a.Action == "PasswordReset");
        resetAudit.ShouldNotBeNull();
        resetAudit.Details?.Contains(resetRes.Value!).ShouldBeFalse();

        // Global check: no audit record contains "password=" with a real secret
        var userAudits = await ctx.AuditLogs.Where(a => a.RecordId == userId).ToListAsync();
        foreach (var audit in userAudits)
        {
            if (audit.Details != null)
            {
                audit.Details.Contains("TemporaryPassword").ShouldBeFalse();
                audit.Details.Contains(resetRes.Value!).ShouldBeFalse();
                audit.Details.Contains(createRes.Value.TemporaryPassword).ShouldBeFalse();
            }
        }
    }
}

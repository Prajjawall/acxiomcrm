using AcxiomCrm.Application.Users;
using AcxiomCrm.Domain.Security;
using AcxiomCrm.Infrastructure.Identity;
using AcxiomCrm.TestSupport;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace AcxiomCrm.Application.Tests;

public class UnlockTests : DbTestBase
{
    private static readonly DateTime FixedDate = new(2026, 10, 19, 10, 0, 0, DateTimeKind.Utc);

    public UnlockTests(SqlServerContainerFixture fixture) : base(fixture)
    {
    }

    [Fact]
    public async Task Unlock_clears_lockout_and_audits()
    {
        await using var ctx = Db.CreateContext();
        var users = await TestUsers.CreateStandardAsync(ctx);

        // Lock out ExecN1
        var execUser = await ctx.Users.FirstAsync(u => u.Id == users.ExecN1Id);
        execUser.LockoutEnd = FixedDate.AddHours(2);
        execUser.AccessFailedCount = 5;
        await ctx.SaveChangesAsync();

        var clock = new FakeClock(FixedDate);
        using var harness = ServiceHarness.Create(Db, users.AdminId, Roles.Admin, clock);
        var service = harness.Services.GetRequiredService<IUserAdminService>();
        var userManager = harness.Services.GetRequiredService<UserManager<ApplicationUser>>();

        var unlockRes = await service.UnlockAsync(users.ExecN1Id);
        unlockRes.Succeeded.ShouldBeTrue();

        var refreshed = await userManager.FindByIdAsync(users.ExecN1Id);
        refreshed.ShouldNotBeNull();
        refreshed.LockoutEnd.ShouldBeNull();
        refreshed.AccessFailedCount.ShouldBe(0);

        var audit = await ctx.AuditLogs
            .Where(a => a.RecordId == users.ExecN1Id && a.Action == "Unlock")
            .FirstOrDefaultAsync();

        audit.ShouldNotBeNull();
        audit.EntityName.ShouldBe("User");
        audit.UserId.ShouldBe(users.AdminId);
    }
}

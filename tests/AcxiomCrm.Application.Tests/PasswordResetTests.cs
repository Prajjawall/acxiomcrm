using AcxiomCrm.Application.Users;
using AcxiomCrm.Domain.Security;
using AcxiomCrm.Infrastructure.Identity;
using AcxiomCrm.TestSupport;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace AcxiomCrm.Application.Tests;

public class PasswordResetTests : DbTestBase
{
    private static readonly DateTime FixedDate = new(2026, 10, 19, 10, 0, 0, DateTimeKind.Utc);

    public PasswordResetTests(SqlServerContainerFixture fixture) : base(fixture)
    {
    }

    [Fact]
    public async Task ResetPassword_clears_lockout_sets_flag_and_audits_without_secret()
    {
        await using var ctx = Db.CreateContext();
        var users = await TestUsers.CreateStandardAsync(ctx);

        var execUser = await ctx.Users.FirstAsync(u => u.Id == users.ExecN1Id);
        execUser.LockoutEnd = FixedDate.AddHours(2);
        execUser.AccessFailedCount = 5;
        var oldStamp = execUser.SecurityStamp;
        await ctx.SaveChangesAsync();

        var clock = new FakeClock(FixedDate);
        using var harness = ServiceHarness.Create(Db, users.AdminId, Roles.Admin, clock);
        var service = harness.Services.GetRequiredService<IUserAdminService>();
        var userManager = harness.Services.GetRequiredService<UserManager<ApplicationUser>>();

        var res = await service.ResetPasswordAsync(users.ExecN1Id);
        res.Succeeded.ShouldBeTrue();
        var tempPassword = res.Value!;
        tempPassword.Length.ShouldBeGreaterThanOrEqualTo(16);

        var refreshed = await userManager.FindByIdAsync(users.ExecN1Id);
        refreshed.ShouldNotBeNull();
        refreshed.MustChangePassword.ShouldBeTrue();
        refreshed.LockoutEnd.ShouldBeNull();
        refreshed.AccessFailedCount.ShouldBe(0);
        refreshed.SecurityStamp.ShouldNotBe(oldStamp);

        // Verify temp password works to check password
        var check = await userManager.CheckPasswordAsync(refreshed, tempPassword);
        check.ShouldBeTrue();

        // Audit assertion: no secret
        var audit = await ctx.AuditLogs
            .Where(a => a.RecordId == users.ExecN1Id && a.Action == "PasswordReset")
            .FirstOrDefaultAsync();

        audit.ShouldNotBeNull();
        audit.Details?.Contains(tempPassword).ShouldBeFalse();
        audit.NewValue?.Contains(tempPassword).ShouldBeFalse();
    }
}

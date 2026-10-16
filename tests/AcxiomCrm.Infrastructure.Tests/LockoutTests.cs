using AcxiomCrm.Application.Abstractions;
using AcxiomCrm.Application.Auth;
using AcxiomCrm.Infrastructure.Identity;
using AcxiomCrm.Infrastructure.Persistence;
using AcxiomCrm.TestSupport;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Shouldly;

namespace AcxiomCrm.Infrastructure.Tests;

public class LockoutTests(SqlServerContainerFixture fixture) : DbTestBase(fixture)
{
    private sealed class CustomTimeProvider(FakeClock clock) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(clock.UtcNow, TimeSpan.Zero);
    }

    private (ServiceProvider sp, IAuthService auth, FakeClock clock, CrmDbContext db, UserManager<ApplicationUser> userManager) CreateHarness()
    {
        var services = new ServiceCollection();
        var initialData = new Dictionary<string, string?>
        {
            ["ConnectionStrings:Default"] = Db.ConnectionString,
            ["Security:Password:MinLength"] = "8",
            ["Security:Lockout:MaxFailedAccessAttempts"] = "5",
            ["Security:Lockout:DefaultLockoutTimeSpanMinutes"] = "15"
        };

        var config = new ConfigurationBuilder().AddInMemoryCollection(initialData).Build();
        services.AddLogging(b => b.AddDebug());
        var clock = new FakeClock(new DateTime(2026, 10, 16, 10, 0, 0, DateTimeKind.Utc));
        services.AddSingleton<IClock>(clock);
        services.AddSingleton<TimeProvider>(new CustomTimeProvider(clock));
        services.AddSingleton<ICurrentUser>(new FakeCurrentUser());

        services.AddInfrastructure(config);

        var sp = services.BuildServiceProvider();
        var db = sp.GetRequiredService<CrmDbContext>();
        var auth = sp.GetRequiredService<IAuthService>();
        var userManager = sp.GetRequiredService<UserManager<ApplicationUser>>();
        return (sp, auth, clock, db, userManager);
    }

    [Fact]
    public async Task Fifth_failure_locks_and_audits_Lockout()
    {
        var (sp, auth, clock, db, userManager) = CreateHarness();
        await using (sp)
        {
            var user = new ApplicationUser
            {
                UserName = "lockout.target",
                Email = "lockout@example.com",
                FullName = "Lockout Target",
                IsActive = true
            };
            await userManager.CreateAsync(user, "CorrectP@ssword1");

            for (var i = 0; i < 4; i++)
            {
                var outcome = await auth.AuthenticateAsync(new LoginCommand("lockout.target", "BadPass123!"));
                outcome.Status.ShouldBe(LoginStatus.InvalidCredentials);
            }

            var fifthOutcome = await auth.AuthenticateAsync(new LoginCommand("lockout.target", "BadPass123!"));
            fifthOutcome.Status.ShouldBe(LoginStatus.LockedOut);
            fifthOutcome.LockoutEndUtc.ShouldNotBeNull();

            var lockoutAudit = await db.AuditLogs
                .AnyAsync(a => a.Action == "Lockout" && a.RecordId == user.Id);
            lockoutAudit.ShouldBeTrue();
        }
    }

    [Fact]
    public async Task Correct_password_while_locked_returns_LockedOut_with_end_time()
    {
        var (sp, auth, clock, db, userManager) = CreateHarness();
        await using (sp)
        {
            var user = new ApplicationUser
            {
                UserName = "locked.user",
                Email = "locked@example.com",
                FullName = "Locked User",
                IsActive = true
            };
            await userManager.CreateAsync(user, "CorrectP@ssword1");

            for (var i = 0; i < 5; i++)
            {
                await auth.AuthenticateAsync(new LoginCommand("locked.user", "BadPass123!"));
            }

            var outcome = await auth.AuthenticateAsync(new LoginCommand("locked.user", "CorrectP@ssword1"));
            outcome.Status.ShouldBe(LoginStatus.LockedOut);
            outcome.LockoutEndUtc.ShouldNotBeNull();
        }
    }

    [Fact]
    public async Task Counter_not_reset_by_attempt_during_lockout()
    {
        var (sp, auth, clock, db, userManager) = CreateHarness();
        await using (sp)
        {
            var user = new ApplicationUser
            {
                UserName = "counter.locked",
                Email = "counter@example.com",
                FullName = "Counter Locked",
                IsActive = true
            };
            await userManager.CreateAsync(user, "CorrectP@ssword1");

            for (var i = 0; i < 5; i++)
            {
                await auth.AuthenticateAsync(new LoginCommand("counter.locked", "BadPass123!"));
            }

            // Attempt during lockout
            await auth.AuthenticateAsync(new LoginCommand("counter.locked", "BadPass123!"));

            var refreshed = await userManager.FindByIdAsync(user.Id);
            refreshed.ShouldNotBeNull();
            (await userManager.IsLockedOutAsync(refreshed)).ShouldBeTrue();
        }
    }

    [Fact]
    public async Task Works_after_lockout_expires()
    {
        var (sp, auth, clock, db, userManager) = CreateHarness();
        await using (sp)
        {
            var user = new ApplicationUser
            {
                UserName = "expires.user",
                Email = "expires@example.com",
                FullName = "Expires User",
                IsActive = true
            };
            await userManager.CreateAsync(user, "CorrectP@ssword1");

            for (var i = 0; i < 5; i++)
            {
                await auth.AuthenticateAsync(new LoginCommand("expires.user", "BadPass123!"));
            }

            // Simulate lockout expiration past the lockout window
            await userManager.SetLockoutEndDateAsync(user, DateTimeOffset.UnixEpoch);

            var outcome = await auth.AuthenticateAsync(new LoginCommand("expires.user", "CorrectP@ssword1"));
            outcome.Status.ShouldBe(LoginStatus.Succeeded);
        }
    }

    [Fact]
    public async Task Success_resets_failed_count()
    {
        var (sp, auth, clock, db, userManager) = CreateHarness();
        await using (sp)
        {
            var user = new ApplicationUser
            {
                UserName = "reset.user",
                Email = "reset@example.com",
                FullName = "Reset User",
                IsActive = true
            };
            await userManager.CreateAsync(user, "CorrectP@ssword1");

            // 3 failed attempts
            for (var i = 0; i < 3; i++)
            {
                await auth.AuthenticateAsync(new LoginCommand("reset.user", "BadPass123!"));
            }

            // Successful login resets counter
            var okOutcome = await auth.AuthenticateAsync(new LoginCommand("reset.user", "CorrectP@ssword1"));
            okOutcome.Status.ShouldBe(LoginStatus.Succeeded);

            var refreshed = await userManager.FindByIdAsync(user.Id);
            refreshed.ShouldNotBeNull();
            refreshed.AccessFailedCount.ShouldBe(0);
        }
    }
}

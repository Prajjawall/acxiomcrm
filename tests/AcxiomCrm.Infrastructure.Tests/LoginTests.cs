using AcxiomCrm.Application.Abstractions;
using AcxiomCrm.Application.Auth;
using AcxiomCrm.Infrastructure.Identity;
using AcxiomCrm.Infrastructure.Persistence;
using AcxiomCrm.TestSupport;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Shouldly;

namespace AcxiomCrm.Infrastructure.Tests;

public class LoginTests(SqlServerContainerFixture fixture) : DbTestBase(fixture)
{
    private sealed class CustomTimeProvider(FakeClock clock) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(clock.UtcNow, TimeSpan.Zero);
    }

    private (ServiceProvider sp, IAuthService auth, FakeClock clock, CrmDbContext db, UserManager<ApplicationUser> userManager) CreateHarness(
        Dictionary<string, string?>? configOverrides = null,
        FakeCurrentUser? currentUser = null)
    {
        var services = new ServiceCollection();
        var initialData = new Dictionary<string, string?>
        {
            ["ConnectionStrings:Default"] = Db.ConnectionString,
            ["Security:Password:MinLength"] = "8",
            ["Security:Lockout:MaxFailedAccessAttempts"] = "5",
            ["Security:Lockout:DefaultLockoutTimeSpanMinutes"] = "15"
        };

        if (configOverrides != null)
        {
            foreach (var (k, v) in configOverrides)
            {
                initialData[k] = v;
            }
        }

        var config = new ConfigurationBuilder().AddInMemoryCollection(initialData).Build();
        services.AddLogging(b => b.AddDebug());
        services.AddSingleton<IConfiguration>(config);
        services.AddInfrastructure(config);

        var clock = new FakeClock(new DateTime(2026, 10, 16, 10, 0, 0, DateTimeKind.Utc));
        services.AddSingleton<IClock>(clock);
        services.AddSingleton<TimeProvider>(new CustomTimeProvider(clock));

        var user = currentUser ?? new FakeCurrentUser();
        services.AddSingleton<ICurrentUser>(user);

        var sp = services.BuildServiceProvider();
        var db = sp.GetRequiredService<CrmDbContext>();
        var auth = sp.GetRequiredService<IAuthService>();
        var userManager = sp.GetRequiredService<UserManager<ApplicationUser>>();
        return (sp, auth, clock, db, userManager);
    }

    [Fact]
    public async Task Succeeds_with_username()
    {
        var (sp, auth, clock, db, userManager) = CreateHarness();
        await using (sp)
        {
            var user = new ApplicationUser
            {
                UserName = "john.doe",
                Email = "john.doe@example.com",
                FullName = "John Doe",
                IsActive = true
            };
            var createRes = await userManager.CreateAsync(user, "SecureP@ss123");
            createRes.Succeeded.ShouldBeTrue();

            var outcome = await auth.AuthenticateAsync(new LoginCommand("john.doe", "SecureP@ss123"));
            outcome.Status.ShouldBe(LoginStatus.Succeeded);
            outcome.UserId.ShouldBe(user.Id);
        }
    }

    [Fact]
    public async Task Succeeds_with_email_in_any_case_and_whitespace()
    {
        var (sp, auth, clock, db, userManager) = CreateHarness();
        await using (sp)
        {
            var user = new ApplicationUser
            {
                UserName = "mary.jane",
                Email = "mary.jane@example.com",
                FullName = "Mary Jane",
                IsActive = true
            };
            var createRes = await userManager.CreateAsync(user, "SecureP@ss123");
            createRes.Succeeded.ShouldBeTrue();

            var outcome = await auth.AuthenticateAsync(new LoginCommand("   MARY.JANE@EXAMPLE.COM   ", "SecureP@ss123"));
            outcome.Status.ShouldBe(LoginStatus.Succeeded);
            outcome.UserId.ShouldBe(user.Id);
        }
    }

    [Fact]
    public async Task Unknown_user_returns_InvalidCredentials()
    {
        var (sp, auth, clock, db, userManager) = CreateHarness();
        await using (sp)
        {
            var outcome = await auth.AuthenticateAsync(new LoginCommand("ghost.user", "AnyPassword123!"));
            outcome.Status.ShouldBe(LoginStatus.InvalidCredentials);
            outcome.UserId.ShouldBeNull();
        }
    }

    [Fact]
    public async Task Wrong_password_returns_InvalidCredentials()
    {
        var (sp, auth, clock, db, userManager) = CreateHarness();
        await using (sp)
        {
            var user = new ApplicationUser
            {
                UserName = "alice.smith",
                Email = "alice@example.com",
                FullName = "Alice Smith",
                IsActive = true
            };
            await userManager.CreateAsync(user, "SecureP@ss123");

            var outcome = await auth.AuthenticateAsync(new LoginCommand("alice.smith", "WrongPassword999!"));
            outcome.Status.ShouldBe(LoginStatus.InvalidCredentials);
        }
    }

    [Fact]
    public async Task Inactive_user_correct_password_returns_InvalidCredentials()
    {
        var (sp, auth, clock, db, userManager) = CreateHarness();
        await using (sp)
        {
            var user = new ApplicationUser
            {
                UserName = "bob.inactive",
                Email = "bob@example.com",
                FullName = "Bob Inactive",
                IsActive = false
            };
            await userManager.CreateAsync(user, "SecureP@ss123");

            var outcome = await auth.AuthenticateAsync(new LoginCommand("bob.inactive", "SecureP@ss123"));
            outcome.Status.ShouldBe(LoginStatus.InvalidCredentials);
        }
    }

    [Fact]
    public async Task MustChangePassword_user_returns_MustChangePassword_status()
    {
        var (sp, auth, clock, db, userManager) = CreateHarness();
        await using (sp)
        {
            var user = new ApplicationUser
            {
                UserName = "charlie.flagged",
                Email = "charlie@example.com",
                FullName = "Charlie Flagged",
                IsActive = true,
                MustChangePassword = true
            };
            await userManager.CreateAsync(user, "SecureP@ss123");

            var outcome = await auth.AuthenticateAsync(new LoginCommand("charlie.flagged", "SecureP@ss123"));
            outcome.Status.ShouldBe(LoginStatus.MustChangePassword);
            outcome.UserId.ShouldBe(user.Id);
        }
    }

    [Fact]
    public async Task Success_sets_LastLoginDate_from_clock()
    {
        var (sp, auth, clock, db, userManager) = CreateHarness();
        await using (sp)
        {
            var expectedTime = new DateTime(2026, 10, 16, 14, 30, 0, DateTimeKind.Utc);
            clock.Set(expectedTime);

            var user = new ApplicationUser
            {
                UserName = "david.clock",
                Email = "david@example.com",
                FullName = "David Clock",
                IsActive = true
            };
            await userManager.CreateAsync(user, "SecureP@ss123");

            var outcome = await auth.AuthenticateAsync(new LoginCommand("david.clock", "SecureP@ss123"));
            outcome.Status.ShouldBe(LoginStatus.Succeeded);

            var refreshed = await userManager.FindByIdAsync(user.Id);
            refreshed.ShouldNotBeNull();
            refreshed.LastLoginDate.ShouldBe(expectedTime);
        }
    }
}

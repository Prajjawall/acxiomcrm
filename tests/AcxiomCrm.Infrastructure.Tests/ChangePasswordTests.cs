using AcxiomCrm.Application.Abstractions;
using AcxiomCrm.Application.Auth;
using AcxiomCrm.Domain.Security;
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

public class ChangePasswordTests(SqlServerContainerFixture fixture) : DbTestBase(fixture)
{
    private (ServiceProvider sp, IAuthService auth, CrmDbContext db, UserManager<ApplicationUser> userManager) CreateHarness(
        FakeCurrentUser currentUser)
    {
        var services = new ServiceCollection();
        var initialData = new Dictionary<string, string?>
        {
            ["ConnectionStrings:Default"] = Db.ConnectionString,
            ["Security:Password:MinLength"] = "8"
        };

        var config = new ConfigurationBuilder().AddInMemoryCollection(initialData).Build();
        services.AddLogging(b => b.AddDebug());
        services.AddSingleton<IConfiguration>(config);
        services.AddInfrastructure(config);

        services.AddSingleton<IClock>(new FakeClock());
        services.AddSingleton<ICurrentUser>(currentUser);

        var sp = services.BuildServiceProvider();
        var db = sp.GetRequiredService<CrmDbContext>();
        var auth = sp.GetRequiredService<IAuthService>();
        var userManager = sp.GetRequiredService<UserManager<ApplicationUser>>();
        return (sp, auth, db, userManager);
    }

    [Fact]
    public async Task Wrong_current_password_fails_on_CurrentPassword()
    {
        var fakeUser = new FakeCurrentUser("user-cp-1", Roles.SalesExecutive, "cp.user1");
        var (sp, auth, db, userManager) = CreateHarness(fakeUser);
        await using (sp)
        {
            var user = new ApplicationUser
            {
                Id = "user-cp-1",
                UserName = "cp.user1",
                Email = "cp1@example.com",
                FullName = "CP User 1",
                IsActive = true
            };
            await userManager.CreateAsync(user, "InitialPass!1");

            var result = await auth.ChangePasswordAsync(new ChangePasswordCommand("WrongOldPass!1", "NewValidPass!2"));
            result.Succeeded.ShouldBeFalse();
            result.Errors.ShouldContain(e => e.Field == "CurrentPassword");
        }
    }

    [Fact]
    public async Task Policy_violation_fails_on_NewPassword()
    {
        var fakeUser = new FakeCurrentUser("user-cp-2", Roles.SalesExecutive, "cp.user2");
        var (sp, auth, db, userManager) = CreateHarness(fakeUser);
        await using (sp)
        {
            var user = new ApplicationUser
            {
                Id = "user-cp-2",
                UserName = "cp.user2",
                Email = "cp2@example.com",
                FullName = "CP User 2",
                IsActive = true
            };
            await userManager.CreateAsync(user, "InitialPass!1");

            // New password too short or weak
            var result = await auth.ChangePasswordAsync(new ChangePasswordCommand("InitialPass!1", "weak"));
            result.Succeeded.ShouldBeFalse();
            result.Errors.ShouldContain(e => e.Field == "NewPassword");
        }
    }

    [Fact]
    public async Task Success_clears_MustChangePassword_and_audits_PasswordChanged()
    {
        var fakeUser = new FakeCurrentUser("user-cp-3", Roles.SalesExecutive, "cp.user3");
        var (sp, auth, db, userManager) = CreateHarness(fakeUser);
        await using (sp)
        {
            var user = new ApplicationUser
            {
                Id = "user-cp-3",
                UserName = "cp.user3",
                Email = "cp3@example.com",
                FullName = "CP User 3",
                IsActive = true,
                MustChangePassword = true
            };
            await userManager.CreateAsync(user, "InitialPass!1");

            var result = await auth.ChangePasswordAsync(new ChangePasswordCommand("InitialPass!1", "BrandNewPass!99"));
            result.Succeeded.ShouldBeTrue();

            var refreshed = await userManager.FindByIdAsync("user-cp-3");
            refreshed.ShouldNotBeNull();
            refreshed.MustChangePassword.ShouldBeFalse();

            var audit = await db.AuditLogs
                .AnyAsync(a => a.Action == "PasswordChanged" && a.RecordId == "user-cp-3");
            audit.ShouldBeTrue();
        }
    }

    [Fact]
    public async Task New_equal_to_current_rejected()
    {
        var fakeUser = new FakeCurrentUser("user-cp-4", Roles.SalesExecutive, "cp.user4");
        var (sp, auth, db, userManager) = CreateHarness(fakeUser);
        await using (sp)
        {
            var user = new ApplicationUser
            {
                Id = "user-cp-4",
                UserName = "cp.user4",
                Email = "cp4@example.com",
                FullName = "CP User 4",
                IsActive = true
            };
            await userManager.CreateAsync(user, "InitialPass!1");

            var result = await auth.ChangePasswordAsync(new ChangePasswordCommand("InitialPass!1", "InitialPass!1"));
            result.Succeeded.ShouldBeFalse();
            result.Errors.ShouldContain(e => e.Field == "NewPassword" && e.Message == "New password cannot be the same as current password.");
        }
    }
}

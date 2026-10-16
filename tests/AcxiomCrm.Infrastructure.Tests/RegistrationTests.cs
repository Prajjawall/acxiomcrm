using AcxiomCrm.Application.Abstractions;
using AcxiomCrm.Application.Auth;
using AcxiomCrm.Application.Common;
using AcxiomCrm.Domain.Security;
using AcxiomCrm.Infrastructure.Identity;
using AcxiomCrm.Infrastructure.Persistence;
using AcxiomCrm.TestSupport;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Shouldly;

namespace AcxiomCrm.Infrastructure.Tests;

public class RegistrationTests(SqlServerContainerFixture fixture) : DbTestBase(fixture)
{
    private async Task<(ServiceProvider sp, IAuthService auth, CrmDbContext db, UserManager<ApplicationUser> userManager)> CreateHarnessAsync(
        string mode = "Open")
    {
        var services = new ServiceCollection();
        var initialData = new Dictionary<string, string?>
        {
            ["ConnectionStrings:Default"] = Db.ConnectionString,
            ["Security:Password:MinLength"] = "8",
            ["Registration:Mode"] = mode
        };

        var config = new ConfigurationBuilder().AddInMemoryCollection(initialData).Build();
        services.AddLogging(b => b.AddDebug());
        services.AddSingleton<IConfiguration>(config);
        services.AddInfrastructure(config);

        services.AddSingleton<IClock>(new FakeClock());
        services.AddSingleton<ICurrentUser>(new FakeCurrentUser());

        var sp = services.BuildServiceProvider();
        var seeder = sp.GetRequiredService<RoleSeeder>();
        await seeder.SeedAsync();

        var db = sp.GetRequiredService<CrmDbContext>();
        var auth = sp.GetRequiredService<IAuthService>();
        var userManager = sp.GetRequiredService<UserManager<ApplicationUser>>();
        return (sp, auth, db, userManager);
    }

    [Fact]
    public async Task Open_mode_creates_active_SalesExecutive()
    {
        var (sp, auth, db, userManager) = await CreateHarnessAsync("Open");
        await using (sp)
        {
            var cmd = new RegisterCommand("Rohan Mehra", "rohan.mehra", "rohan@example.com", "Str0ng!P@ss123");
            var result = await auth.RegisterAsync(cmd);

            result.Succeeded.ShouldBeTrue();
            result.Value.ShouldNotBeNull();

            var user = await userManager.FindByIdAsync(result.Value!);
            user.ShouldNotBeNull();
            user.IsActive.ShouldBeTrue();
            user.FullName.ShouldBe("Rohan Mehra");

            var roles = await userManager.GetRolesAsync(user);
            roles.ShouldContain(Roles.SalesExecutive);
        }
    }

    [Fact]
    public async Task Role_cannot_be_chosen()
    {
        var (sp, auth, db, userManager) = await CreateHarnessAsync("Open");
        await using (sp)
        {
            // RegisterCommand contract has no role parameter
            var cmd = new RegisterCommand("Sneha Roy", "sneha.roy", "sneha@example.com", "Str0ng!P@ss123");
            var result = await auth.RegisterAsync(cmd);

            result.Succeeded.ShouldBeTrue();
            var user = await userManager.FindByIdAsync(result.Value!);
            user.ShouldNotBeNull();

            var roles = await userManager.GetRolesAsync(user);
            roles.Count.ShouldBe(1);
            roles.ShouldContain(Roles.SalesExecutive);
            roles.ShouldNotContain(Roles.Admin);
            roles.ShouldNotContain(Roles.Manager);
        }
    }

    [Fact]
    public async Task ApprovalRequired_creates_inactive_user()
    {
        var (sp, auth, db, userManager) = await CreateHarnessAsync("ApprovalRequired");
        await using (sp)
        {
            var cmd = new RegisterCommand("Inactive Guy", "inactive.guy", "inactive@example.com", "Str0ng!P@ss123");
            var result = await auth.RegisterAsync(cmd);

            result.Succeeded.ShouldBeTrue();
            var user = await userManager.FindByIdAsync(result.Value!);
            user.ShouldNotBeNull();
            user.IsActive.ShouldBeFalse();
        }
    }

    [Fact]
    public async Task Disabled_returns_NotFound_result()
    {
        var (sp, auth, db, userManager) = await CreateHarnessAsync("Disabled");
        await using (sp)
        {
            var cmd = new RegisterCommand("Disabled Reg", "disabled.reg", "disabled@example.com", "Str0ng!P@ss123");
            var result = await auth.RegisterAsync(cmd);

            result.Succeeded.ShouldBeFalse();
            result.Kind.ShouldBe(ErrorKind.NotFound);
        }
    }

    [Fact]
    public async Task Duplicate_email_returns_validation_error_on_Email()
    {
        var (sp, auth, db, userManager) = await CreateHarnessAsync("Open");
        await using (sp)
        {
            var first = new RegisterCommand("First User", "first.user", "dup@example.com", "Str0ng!P@ss123");
            (await auth.RegisterAsync(first)).Succeeded.ShouldBeTrue();

            var second = new RegisterCommand("Second User", "second.user", "dup@example.com", "Str0ng!P@ss123");
            var result = await auth.RegisterAsync(second);

            result.Succeeded.ShouldBeFalse();
            result.Errors.ShouldContain(e => e.Field == "Email");
        }
    }

    [Fact]
    public async Task Duplicate_username_returns_validation_error_on_UserName()
    {
        var (sp, auth, db, userManager) = await CreateHarnessAsync("Open");
        await using (sp)
        {
            var first = new RegisterCommand("First User", "duplicate.name", "first@example.com", "Str0ng!P@ss123");
            (await auth.RegisterAsync(first)).Succeeded.ShouldBeTrue();

            var second = new RegisterCommand("Second User", "duplicate.name", "second@example.com", "Str0ng!P@ss123");
            var result = await auth.RegisterAsync(second);

            result.Succeeded.ShouldBeFalse();
            result.Errors.ShouldContain(e => e.Field == "UserName");
        }
    }

    [Fact]
    public async Task Weak_password_returns_validation_error_on_Password_with_identity_messages()
    {
        var (sp, auth, db, userManager) = await CreateHarnessAsync("Open");
        await using (sp)
        {
            var cmd = new RegisterCommand("Weak Pwd", "weak.pwd", "weak@example.com", "sh0rt");
            var result = await auth.RegisterAsync(cmd);

            result.Succeeded.ShouldBeFalse();
            result.Errors.ShouldContain(e => e.Field == "Password");
        }
    }

    [Fact]
    public async Task Fields_validated()
    {
        var (sp, auth, db, userManager) = await CreateHarnessAsync("Open");
        await using (sp)
        {
            // Empty full name
            var emptyName = new RegisterCommand("", "user.valid", "valid@example.com", "Str0ng!P@ss123");
            var nameResult = await auth.RegisterAsync(emptyName);
            nameResult.Succeeded.ShouldBeFalse();
            nameResult.Errors.ShouldContain(e => e.Field == "FullName" && e.Message == "Full name is required.");

            // Invalid email
            var badEmail = new RegisterCommand("Valid Name", "user.bademail", "not-an-email", "Str0ng!P@ss123");
            var emailResult = await auth.RegisterAsync(badEmail);
            emailResult.Succeeded.ShouldBeFalse();
            emailResult.Errors.ShouldContain(e => e.Field == "Email" && e.Message == "Invalid email format.");
        }
    }
}

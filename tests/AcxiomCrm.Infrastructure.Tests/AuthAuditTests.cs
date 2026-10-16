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

public class AuthAuditTests(SqlServerContainerFixture fixture) : DbTestBase(fixture)
{
    [Fact]
    public async Task No_audit_row_contains_the_password_or_hash()
    {
        var services = new ServiceCollection();
        var initialData = new Dictionary<string, string?>
        {
            ["ConnectionStrings:Default"] = Db.ConnectionString,
            ["Security:Password:MinLength"] = "8",
            ["Security:Lockout:MaxFailedAccessAttempts"] = "5",
            ["Security:Lockout:DefaultLockoutTimeSpanMinutes"] = "15",
            ["Registration:Mode"] = "Open"
        };

        var config = new ConfigurationBuilder().AddInMemoryCollection(initialData).Build();
        services.AddLogging(b => b.AddDebug());
        services.AddSingleton<IConfiguration>(config);
        services.AddInfrastructure(config);

        services.AddSingleton<IClock>(new FakeClock());
        var currentUser = new FakeCurrentUser();
        services.AddSingleton<ICurrentUser>(currentUser);

        var sp = services.BuildServiceProvider();
        await using (sp)
        {
            var seeder = sp.GetRequiredService<RoleSeeder>();
            await seeder.SeedAsync();

            var db = sp.GetRequiredService<CrmDbContext>();
            var auth = sp.GetRequiredService<IAuthService>();
            var userManager = sp.GetRequiredService<UserManager<ApplicationUser>>();

            const string secretPassword1 = "SuperSecretPassword1!";
            const string secretPassword2 = "SuperSecretPassword2!";
            const string badAttemptPassword = "AttemptSecret999!";

            // 1. Register
            var regRes = await auth.RegisterAsync(new RegisterCommand("Audit Test User", "audit.test", "audit@example.com", secretPassword1));
            regRes.Succeeded.ShouldBeTrue();
            var userId = regRes.Value!;

            var createdUser = await userManager.FindByIdAsync(userId);
            createdUser.ShouldNotBeNull();
            var passwordHash = createdUser.PasswordHash!;

            // 2. Failed login
            await auth.AuthenticateAsync(new LoginCommand("audit.test", badAttemptPassword));

            // 3. Succeeded login
            await auth.AuthenticateAsync(new LoginCommand("audit.test", secretPassword1));

            // 4. Change password
            currentUser.UserId = userId;
            currentUser.Role = Roles.SalesExecutive;
            currentUser.IsAuthenticated = true;
            var cpRes = await auth.ChangePasswordAsync(new ChangePasswordCommand(secretPassword1, secretPassword2));
            cpRes.Succeeded.ShouldBeTrue();

            // 5. Logout
            await auth.RecordLogoutAsync();

            // Scan all audit log entries
            var allAuditLogs = await db.AuditLogs.AsNoTracking().ToListAsync();
            allAuditLogs.Count.ShouldBeGreaterThan(0);

            foreach (var log in allAuditLogs)
            {
                var combined = $"{log.Action} {log.Module} {log.EntityName} {log.RecordId} {log.OldValue} {log.NewValue} {log.Details}";
                combined.ShouldNotContain(secretPassword1);
                combined.ShouldNotContain(secretPassword2);
                combined.ShouldNotContain(badAttemptPassword);
                combined.ShouldNotContain(passwordHash);
            }
        }
    }
}

using AcxiomCrm.Application.Common;
using AcxiomCrm.Application.Users;
using AcxiomCrm.Domain.Security;
using AcxiomCrm.TestSupport;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace AcxiomCrm.Application.Tests;

public class LastAdminGuardTests : DbTestBase
{
    private static readonly DateTime FixedDate = new(2026, 10, 19, 10, 0, 0, DateTimeKind.Utc);

    public LastAdminGuardTests(SqlServerContainerFixture fixture) : base(fixture)
    {
    }

    [Fact]
    public async Task Admin_cannot_demote_themselves()
    {
        await using var ctx = Db.CreateContext();
        var users = await TestUsers.CreateStandardAsync(ctx);

        var clock = new FakeClock(FixedDate);
        using var harness = ServiceHarness.Create(Db, users.AdminId, Roles.Admin, clock);
        var service = harness.Services.GetRequiredService<IUserAdminService>();

        var cmd = new UpdateUserCommand("Admin User", "admin", "admin@acxiom.com", Roles.Manager, null);
        var res = await service.UpdateAsync(users.AdminId, cmd);

        res.Succeeded.ShouldBeFalse();
        res.Kind.ShouldBe(ErrorKind.Validation);
        res.Errors.ShouldContain(e => e.Message == "Cannot change your own role.");
    }

    [Fact]
    public async Task Admin_cannot_deactivate_themselves()
    {
        await using var ctx = Db.CreateContext();
        var users = await TestUsers.CreateStandardAsync(ctx);

        var clock = new FakeClock(FixedDate);
        using var harness = ServiceHarness.Create(Db, users.AdminId, Roles.Admin, clock);
        var service = harness.Services.GetRequiredService<IUserAdminService>();

        var cmd = new DeactivateUserCommand();
        var res = await service.DeactivateAsync(users.AdminId, cmd);

        res.Succeeded.ShouldBeFalse();
        res.Kind.ShouldBe(ErrorKind.Validation);
        res.Errors.ShouldContain(e => e.Message == "Cannot deactivate your own account.");
    }

    [Fact]
    public async Task Last_active_admin_cannot_be_demoted()
    {
        await using var ctx = Db.CreateContext();
        var users = await TestUsers.CreateStandardAsync(ctx);

        var clock = new FakeClock(FixedDate);
        // Another user who is Admin (e.g. creating admin2 then acting as admin2 trying to demote admin1)
        using var harness = ServiceHarness.Create(Db, users.AdminId, Roles.Admin, clock);
        var service = harness.Services.GetRequiredService<IUserAdminService>();

        // Create a 2nd admin
        var createAdmin2 = await service.CreateAsync(new CreateUserCommand("Admin Two", "admin2", "admin2@acxiom.com", Roles.Admin, null, true));
        createAdmin2.Succeeded.ShouldBeTrue();
        var admin2Id = createAdmin2.Value!.UserId;

        // Harness as Admin2
        using var harness2 = ServiceHarness.Create(Db, admin2Id, Roles.Admin, clock);
        var service2 = harness2.Services.GetRequiredService<IUserAdminService>();

        // Admin2 demotes Admin1 -> Succeeded because Admin2 remains as active admin!
        var demoteAdmin1 = await service2.UpdateAsync(users.AdminId, new UpdateUserCommand("Admin User", "admin", "admin@acxiom.com", Roles.Manager, null));
        demoteAdmin1.Succeeded.ShouldBeTrue();

        // Now Admin2 is the LAST remaining active admin!
        // A 3rd admin isn't there, and if Admin2 tries to be demoted by anyone or deactivated:
        var demoteLastAdmin = await service.UpdateAsync(admin2Id, new UpdateUserCommand("Admin Two", "admin2", "admin2@acxiom.com", Roles.Manager, null));
        demoteLastAdmin.Succeeded.ShouldBeFalse();
        demoteLastAdmin.Kind.ShouldBe(ErrorKind.Validation);
        demoteLastAdmin.Errors.ShouldContain(e => e.Message == "At least one active administrator is required.");
    }

    [Fact]
    public async Task Last_active_admin_cannot_be_deactivated()
    {
        await using var ctx = Db.CreateContext();
        var users = await TestUsers.CreateStandardAsync(ctx);

        var clock = new FakeClock(FixedDate);
        using var harness = ServiceHarness.Create(Db, users.AdminId, Roles.Admin, clock);
        var service = harness.Services.GetRequiredService<IUserAdminService>();

        // Create 2nd admin
        var createAdmin2 = await service.CreateAsync(new CreateUserCommand("Admin Two", "admin2", "admin2@acxiom.com", Roles.Admin, null, true));
        var admin2Id = createAdmin2.Value!.UserId;

        // Admin 1 deactivates Admin 2 -> Succeeded (Admin 1 remains)
        var deactAdmin2 = await service.DeactivateAsync(admin2Id, new DeactivateUserCommand());
        deactAdmin2.Succeeded.ShouldBeTrue();

        // Now Admin 1 is the ONLY active admin left. Deactivating Admin 1:
        // Even if Admin 2 tries:
        using var harness2 = ServiceHarness.Create(Db, admin2Id, Roles.Admin, clock);
        var service2 = harness2.Services.GetRequiredService<IUserAdminService>();

        var deactLastAdmin = await service2.DeactivateAsync(users.AdminId, new DeactivateUserCommand());
        deactLastAdmin.Succeeded.ShouldBeFalse();
        deactLastAdmin.Kind.ShouldBe(ErrorKind.Validation);
        deactLastAdmin.Errors.ShouldContain(e => e.Message == "At least one active administrator is required.");
    }

    [Fact]
    public async Task Manager_with_direct_reports_cannot_be_demoted_or_deactivated_without_reassignment()
    {
        await using var ctx = Db.CreateContext();
        var users = await TestUsers.CreateStandardAsync(ctx);

        var clock = new FakeClock(FixedDate);
        using var harness = ServiceHarness.Create(Db, users.AdminId, Roles.Admin, clock);
        var service = harness.Services.GetRequiredService<IUserAdminService>();

        // NorthManagerId has active report ExecN1Id!
        // 1. Demoting NorthManager to SalesExecutive
        var demoteCmd = new UpdateUserCommand("North Manager", "mgr.north", "mgr.north@acxiom.com", Roles.SalesExecutive, users.SouthManagerId);
        var demoteRes = await service.UpdateAsync(users.NorthManagerId, demoteCmd);
        demoteRes.Succeeded.ShouldBeFalse();
        demoteRes.Kind.ShouldBe(ErrorKind.Validation);
        demoteRes.Errors.ShouldContain(e => e.Message == "Reassign this manager's team first.");

        // 2. Deactivating NorthManager
        var deactCmd = new DeactivateUserCommand();
        var deactRes = await service.DeactivateAsync(users.NorthManagerId, deactCmd);
        deactRes.Succeeded.ShouldBeFalse();
        deactRes.Kind.ShouldBe(ErrorKind.Validation);
        deactRes.Errors.ShouldContain(e => e.Message == "Reassign this manager's team first.");
    }
}

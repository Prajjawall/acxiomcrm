using AcxiomCrm.Application.Common;
using AcxiomCrm.Application.Users;
using AcxiomCrm.Domain.Security;
using AcxiomCrm.TestSupport;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace AcxiomCrm.Application.Tests;

public class UserAdminAuthorisationTests : DbTestBase
{
    private static readonly DateTime FixedDate = new(2026, 10, 19, 10, 0, 0, DateTimeKind.Utc);

    public UserAdminAuthorisationTests(SqlServerContainerFixture fixture) : base(fixture)
    {
    }

    [Fact]
    public async Task Manager_cannot_administer_users()
    {
        await using var ctx = Db.CreateContext();
        var users = await TestUsers.CreateStandardAsync(ctx);

        var clock = new FakeClock(FixedDate);
        using var harness = ServiceHarness.Create(Db, users.NorthManagerId, Roles.Manager, clock);
        var service = harness.Services.GetRequiredService<IUserAdminService>();

        // Create
        var createRes = await service.CreateAsync(new CreateUserCommand("U", "u", "u@acxiom.com", Roles.SalesExecutive, null, true));
        createRes.Succeeded.ShouldBeFalse();
        createRes.Kind.ShouldBe(ErrorKind.Forbidden);

        // Update
        var updateRes = await service.UpdateAsync(users.ExecN1Id, new UpdateUserCommand("Exec 1", "exec.n1", "exec.n1@acxiom.com", Roles.SalesExecutive, users.NorthManagerId));
        updateRes.Succeeded.ShouldBeFalse();
        updateRes.Kind.ShouldBe(ErrorKind.Forbidden);

        // Deactivate
        var deactRes = await service.DeactivateAsync(users.ExecN1Id, new DeactivateUserCommand());
        deactRes.Succeeded.ShouldBeFalse();
        deactRes.Kind.ShouldBe(ErrorKind.Forbidden);

        // Activate
        var actRes = await service.ActivateAsync(users.ExecN1Id);
        actRes.Succeeded.ShouldBeFalse();
        actRes.Kind.ShouldBe(ErrorKind.Forbidden);

        // Unlock
        var unlockRes = await service.UnlockAsync(users.ExecN1Id);
        unlockRes.Succeeded.ShouldBeFalse();
        unlockRes.Kind.ShouldBe(ErrorKind.Forbidden);

        // ResetPassword
        var resetRes = await service.ResetPasswordAsync(users.ExecN1Id);
        resetRes.Succeeded.ShouldBeFalse();
        resetRes.Kind.ShouldBe(ErrorKind.Forbidden);
    }

    [Fact]
    public async Task Manager_can_only_view_their_team()
    {
        await using var ctx = Db.CreateContext();
        var users = await TestUsers.CreateStandardAsync(ctx);

        var clock = new FakeClock(FixedDate);
        using var harness = ServiceHarness.Create(Db, users.NorthManagerId, Roles.Manager, clock);
        var service = harness.Services.GetRequiredService<IUserAdminService>();

        // 1. Can view self
        var selfRes = await service.GetAsync(users.NorthManagerId);
        selfRes.Succeeded.ShouldBeTrue();

        // 2. Can view direct report (ExecN1Id)
        var reportRes = await service.GetAsync(users.ExecN1Id);
        reportRes.Succeeded.ShouldBeTrue();

        // 3. Cannot view user from another team (ExecS1Id is in South team) -> NotFound
        var otherTeamRes = await service.GetAsync(users.ExecS1Id);
        otherTeamRes.Succeeded.ShouldBeFalse();
        otherTeamRes.Kind.ShouldBe(ErrorKind.NotFound);

        // 4. Search only returns team members
        var searchRes = await service.SearchAsync(new UserFilter(), new PageQuery(1, 50));
        searchRes.Items.Any(i => i.Id == users.NorthManagerId).ShouldBeTrue();
        searchRes.Items.Any(i => i.Id == users.ExecN1Id).ShouldBeTrue();
        searchRes.Items.Any(i => i.Id == users.ExecS1Id).ShouldBeFalse();
        searchRes.Items.Any(i => i.Id == users.SouthManagerId).ShouldBeFalse();
    }

    [Fact]
    public async Task SalesExecutive_is_forbidden_from_users_module()
    {
        await using var ctx = Db.CreateContext();
        var users = await TestUsers.CreateStandardAsync(ctx);

        var clock = new FakeClock(FixedDate);
        using var harness = ServiceHarness.Create(Db, users.ExecN1Id, Roles.SalesExecutive, clock);
        var service = harness.Services.GetRequiredService<IUserAdminService>();

        var getRes = await service.GetAsync(users.ExecN1Id);
        getRes.Succeeded.ShouldBeFalse();
        getRes.Kind.ShouldBe(ErrorKind.Forbidden);

        var searchRes = await service.SearchAsync(new UserFilter(), new PageQuery(1, 10));
        searchRes.Total.ShouldBe(0);
        searchRes.Items.ShouldBeEmpty();
    }
}

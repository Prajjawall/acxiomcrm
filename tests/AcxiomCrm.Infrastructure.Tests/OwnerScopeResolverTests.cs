using AcxiomCrm.Domain.Security;
using AcxiomCrm.Infrastructure.Scope;
using AcxiomCrm.TestSupport;
using Shouldly;

namespace AcxiomCrm.Infrastructure.Tests;

public class OwnerScopeResolverTests : DbTestBase
{
    public OwnerScopeResolverTests(SqlServerContainerFixture fixture) : base(fixture)
    {
    }

    [Fact]
    public async Task Admin_is_All()
    {
        await using var ctx = Db.CreateContext();
        var users = await TestUsers.CreateStandardAsync(ctx);

        var currentUser = new FakeCurrentUser(users.AdminId, Roles.Admin, "admin");
        var resolver = new OwnerScopeResolver(currentUser, ctx);

        var scope = await resolver.ResolveAsync();

        scope.All.ShouldBeTrue();
        scope.OwnerIds.ShouldBeEmpty();
    }

    [Fact]
    public async Task Manager_scope_is_self_plus_direct_reports()
    {
        await using var ctx = Db.CreateContext();
        var users = await TestUsers.CreateStandardAsync(ctx);

        var currentUser = new FakeCurrentUser(users.NorthManagerId, Roles.Manager, "mgr.north");
        var resolver = new OwnerScopeResolver(currentUser, ctx);

        var scope = await resolver.ResolveAsync();

        scope.All.ShouldBeFalse();
        scope.OwnerIds.Count.ShouldBe(3);
        scope.OwnerIds.ShouldContain(users.NorthManagerId);
        scope.OwnerIds.ShouldContain(users.ExecN1Id);
        scope.OwnerIds.ShouldContain(users.ExecN2Id);
        scope.OwnerIds.ShouldNotContain(users.ExecS1Id);
    }

    [Fact]
    public async Task Manager_scope_includes_inactive_reports()
    {
        await using var ctx = Db.CreateContext();
        var users = await TestUsers.CreateStandardAsync(ctx);

        var currentUser = new FakeCurrentUser(users.NorthManagerId, Roles.Manager, "mgr.north");
        var resolver = new OwnerScopeResolver(currentUser, ctx);

        var scope = await resolver.ResolveAsync();

        // ExecN2 is inactive, but should still be in the manager's scope
        scope.OwnerIds.ShouldContain(users.ExecN2Id);
    }

    [Fact]
    public async Task Executive_scope_is_self_only()
    {
        await using var ctx = Db.CreateContext();
        var users = await TestUsers.CreateStandardAsync(ctx);

        var currentUser = new FakeCurrentUser(users.ExecN1Id, Roles.SalesExecutive, "exec.n1");
        var resolver = new OwnerScopeResolver(currentUser, ctx);

        var scope = await resolver.ResolveAsync();

        scope.All.ShouldBeFalse();
        scope.OwnerIds.Count.ShouldBe(1);
        scope.OwnerIds.ShouldContain(users.ExecN1Id);
    }

    [Fact]
    public async Task Anonymous_scope_is_empty_not_All()
    {
        await using var ctx = Db.CreateContext();
        var currentUser = new FakeCurrentUser(); // anonymous
        var resolver = new OwnerScopeResolver(currentUser, ctx);

        var scope = await resolver.ResolveAsync();

        scope.All.ShouldBeFalse();
        scope.OwnerIds.ShouldBeEmpty();
    }

    [Fact]
    public async Task Result_is_cached_within_scope()
    {
        await using var ctx = Db.CreateContext();
        var users = await TestUsers.CreateStandardAsync(ctx);

        var currentUser = new FakeCurrentUser(users.NorthManagerId, Roles.Manager, "mgr.north");
        var resolver = new OwnerScopeResolver(currentUser, ctx);

        var scope1 = await resolver.ResolveAsync();
        var scope2 = await resolver.ResolveAsync();

        ReferenceEquals(scope1, scope2).ShouldBeTrue();
    }
}

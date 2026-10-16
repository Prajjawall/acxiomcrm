using System.Reflection;
using AcxiomCrm.Application.Abstractions;
using AcxiomCrm.Application.Users;
using AcxiomCrm.Domain.Security;
using AcxiomCrm.Infrastructure.Users;
using NSubstitute;
using Shouldly;

namespace AcxiomCrm.Application.Tests;

public class UserDirectoryTests
{
    private readonly ICrmDbContext _db = Substitute.For<ICrmDbContext>();
    private readonly ICurrentUser _currentUser = Substitute.For<ICurrentUser>();

    private readonly List<UserSummary> _allUsers =
    [
        new("admin-1", "Admin One", "admin1", "admin1@example.com", Roles.Admin, true, null, false, null, false, new DateTime(2026, 10, 16, 12, 0, 0, DateTimeKind.Utc), null),
        new("mgr-1", "Manager One", "mgr1", "mgr1@example.com", Roles.Manager, true, null, false, null, false, new DateTime(2026, 10, 16, 12, 0, 0, DateTimeKind.Utc), null),
        new("mgr-2", "Manager Two", "mgr2", "mgr2@example.com", Roles.Manager, true, null, false, null, false, new DateTime(2026, 10, 16, 12, 0, 0, DateTimeKind.Utc), null),
        new("exec-1", "Exec One", "exec1", "exec1@example.com", Roles.SalesExecutive, true, "mgr-1", false, null, false, new DateTime(2026, 10, 16, 12, 0, 0, DateTimeKind.Utc), null),
        new("exec-2", "Exec Two", "exec2", "exec2@example.com", Roles.SalesExecutive, true, "mgr-1", false, null, false, new DateTime(2026, 10, 16, 12, 0, 0, DateTimeKind.Utc), null),
        new("exec-3", "Exec Three", "exec3", "exec3@example.com", Roles.SalesExecutive, true, "mgr-2", false, null, false, new DateTime(2026, 10, 16, 12, 0, 0, DateTimeKind.Utc), null),
        new("deactivated-exec", "Deactivated Exec", "deact", "deact@example.com", Roles.SalesExecutive, false, "mgr-1", false, null, false, new DateTime(2026, 10, 16, 12, 0, 0, DateTimeKind.Utc), null)
    ];

    private UserDirectory CreateDirectory()
    {
        _db.Users.Returns(_allUsers.AsQueryable());
        return new UserDirectory(_db, _currentUser);
    }

    [Fact]
    public async Task Admin_lists_all_active_managers_and_execs()
    {
        _currentUser.IsAuthenticated.Returns(true);
        _currentUser.UserId.Returns("admin-1");
        _currentUser.Role.Returns(Roles.Admin);

        var dir = CreateDirectory();
        var options = await dir.ListAssignableOwnersAsync();

        options.Count.ShouldBe(5);
        options.ShouldNotContain(o => o.Id == "admin-1");
        options.ShouldNotContain(o => o.Id == "deactivated-exec");
        options.ShouldContain(o => o.Id == "mgr-1");
        options.ShouldContain(o => o.Id == "mgr-2");
        options.ShouldContain(o => o.Id == "exec-1");
        options.ShouldContain(o => o.Id == "exec-2");
        options.ShouldContain(o => o.Id == "exec-3");
    }

    [Fact]
    public async Task Manager_lists_self_and_team_only()
    {
        _currentUser.IsAuthenticated.Returns(true);
        _currentUser.UserId.Returns("mgr-1");
        _currentUser.Role.Returns(Roles.Manager);

        var dir = CreateDirectory();
        var options = await dir.ListAssignableOwnersAsync();

        options.Count.ShouldBe(3);
        options.ShouldContain(o => o.Id == "mgr-1");
        options.ShouldContain(o => o.Id == "exec-1");
        options.ShouldContain(o => o.Id == "exec-2");
        options.ShouldNotContain(o => o.Id == "exec-3");
        options.ShouldNotContain(o => o.Id == "deactivated-exec");
    }

    [Fact]
    public async Task Executive_lists_only_self()
    {
        _currentUser.IsAuthenticated.Returns(true);
        _currentUser.UserId.Returns("exec-1");
        _currentUser.Role.Returns(Roles.SalesExecutive);

        var dir = CreateDirectory();
        var options = await dir.ListAssignableOwnersAsync();

        options.Count.ShouldBe(1);
        options[0].Id.ShouldBe("exec-1");
    }

    [Fact]
    public async Task Deactivated_user_excluded()
    {
        _currentUser.IsAuthenticated.Returns(true);
        _currentUser.UserId.Returns("mgr-1");
        _currentUser.Role.Returns(Roles.Manager);

        var dir = CreateDirectory();
        var options = await dir.ListAssignableOwnersAsync();

        options.ShouldNotContain(o => o.Id == "deactivated-exec");
    }

    [Fact]
    public void No_property_of_UserOption_or_UserSummary_contains_sensitive_keywords()
    {
        var sensitive = new[] { "Hash", "Stamp", "Token", "Password" };

        var optionProps = typeof(UserOption).GetProperties(BindingFlags.Public | BindingFlags.Instance);
        foreach (var p in optionProps)
        {
            sensitive.ShouldNotContain(s => p.Name.Contains(s, StringComparison.OrdinalIgnoreCase));
        }

        var summaryProps = typeof(UserSummary).GetProperties(BindingFlags.Public | BindingFlags.Instance);
        foreach (var p in summaryProps)
        {
            if (p.Name == "MustChangePassword")
            {
                continue; // Flag, not secret
            }
            sensitive.ShouldNotContain(s => p.Name.Contains(s, StringComparison.OrdinalIgnoreCase));
        }
    }
}

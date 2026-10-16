using System.Security.Claims;
using AcxiomCrm.Domain.Security;
using AcxiomCrm.Web.Navigation;
using Shouldly;

namespace AcxiomCrm.Web.Tests;

public class NavigationVisibilityTests
{
    private static ClaimsPrincipal CreateUserWithRole(string role)
    {
        var identity = new ClaimsIdentity([new Claim(ClaimTypes.Role, role)], "TestAuth");
        return new ClaimsPrincipal(identity);
    }

    [Fact]
    public void Admin_sees_Administration_group()
    {
        var user = CreateUserWithRole(Roles.Admin);
        var groups = NavigationProvider.GetFor(user);

        groups.ShouldContain(g => g.Title == "Administration");
        var adminGroup = groups.First(g => g.Title == "Administration");
        adminGroup.Items.ShouldContain(i => i.Label == "Users");
        adminGroup.Items.ShouldContain(i => i.Label == "Roles");
        adminGroup.Items.ShouldContain(i => i.Label == "Audit Log");
    }

    [Fact]
    public void Manager_sees_Sales_Activity_Insights_and_no_Administration()
    {
        var user = CreateUserWithRole(Roles.Manager);
        var groups = NavigationProvider.GetFor(user);

        groups.ShouldContain(g => g.Title == "Workspace");
        groups.ShouldContain(g => g.Title == "Sales");
        groups.ShouldContain(g => g.Title == "Activity");
        groups.ShouldContain(g => g.Title == "Insights");
        groups.ShouldNotContain(g => g.Title == "Administration");
    }

    [Fact]
    public void SalesExecutive_sees_Workspace_Sales_Activity_Insights_only()
    {
        var user = CreateUserWithRole(Roles.SalesExecutive);
        var groups = NavigationProvider.GetFor(user);

        groups.ShouldContain(g => g.Title == "Workspace");
        groups.ShouldContain(g => g.Title == "Sales");
        groups.ShouldContain(g => g.Title == "Activity");
        groups.ShouldContain(g => g.Title == "Insights");
        groups.ShouldNotContain(g => g.Title == "Administration");
    }

    [Fact]
    public void Anonymous_user_sees_no_nav_groups()
    {
        var user = new ClaimsPrincipal(new ClaimsIdentity());
        var groups = NavigationProvider.GetFor(user);

        groups.ShouldBeEmpty();
    }
}

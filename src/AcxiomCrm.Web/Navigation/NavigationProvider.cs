using System.Security.Claims;
using AcxiomCrm.Domain.Security;

namespace AcxiomCrm.Web.Navigation;

public static class NavigationProvider
{
    private static readonly List<NavGroupDefinition> Groups =
    [
        new("Workspace",
        [
            new("Dashboard", "layout-dashboard", "Dashboard", "Index", Permissions.DashboardView)
        ]),
        new("Sales",
        [
            new("Customers", "users", "Customers", "Index", Permissions.CustomersView),
            new("Leads", "target", "Leads", "Index", Permissions.LeadsView),
            new("Opportunities", "briefcase", "Opportunities", "Index", Permissions.OpportunitiesView)
        ]),
        new("Activity",
        [
            new("Follow-ups", "calendar-clock", "FollowUps", "Index", Permissions.FollowUpsView),
            new("Activities", "activity", "Activities", "Index", Permissions.ActivitiesView)
        ]),
        new("Insights",
        [
            new("Reports", "bar-chart-3", "Reports", "Index", Permissions.ReportsCustomers)
        ]),
        new("Administration",
        [
            new("Users", "user-cog", "Users", "Index", Permissions.UsersView),
            new("Roles", "shield", "Roles", "Index", Permissions.RolesView),
            new("Audit Log", "clipboard-list", "Audit", "Index", Permissions.AuditView)
        ])
    ];

    public static IReadOnlyList<NavGroup> GetFor(ClaimsPrincipal user)
    {
        var role = user.FindFirstValue(ClaimTypes.Role);
        if (string.IsNullOrEmpty(role))
        {
            return [];
        }

        var result = new List<NavGroup>();

        foreach (var group in Groups)
        {
            var visibleItems = new List<NavItem>();
            foreach (var item in group.Items)
            {
                if (item.Enabled && RolePermissionMap.Has(role, item.Permission))
                {
                    visibleItems.Add(item);
                }
            }

            if (visibleItems.Count > 0)
            {
                result.Add(new NavGroup(group.Title, visibleItems));
            }
        }

        return result;
    }

    private sealed record NavGroupDefinition(string Title, List<NavItem> Items);
}

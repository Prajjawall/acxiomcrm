namespace AcxiomCrm.Domain.Security;

public static class RolePermissionMap
{
    private static readonly HashSet<string> AdminPermissions = new(Permissions.All, StringComparer.Ordinal);

    private static readonly HashSet<string> ManagerPermissions = new(StringComparer.Ordinal)
    {
        Permissions.DashboardView,
        Permissions.CustomersView,
        Permissions.CustomersCreate,
        Permissions.CustomersEdit,
        Permissions.CustomersDelete,
        Permissions.LeadsView,
        Permissions.LeadsCreate,
        Permissions.LeadsEdit,
        Permissions.LeadsDelete,
        Permissions.LeadsAssign,
        Permissions.LeadsConvert,
        Permissions.OpportunitiesView,
        Permissions.OpportunitiesCreate,
        Permissions.OpportunitiesEdit,
        Permissions.OpportunitiesDelete,
        Permissions.OpportunitiesReopen,
        Permissions.FollowUpsView,
        Permissions.FollowUpsCreate,
        Permissions.FollowUpsEdit,
        Permissions.FollowUpsDelete,
        Permissions.FollowUpsHistorical,
        Permissions.ActivitiesView,
        Permissions.ActivitiesCreate,
        Permissions.ActivitiesEdit,
        Permissions.ActivitiesDelete,
        Permissions.UsersView,
        Permissions.AuditViewTeam,
        Permissions.ReportsCustomers,
        Permissions.ReportsLeads,
        Permissions.ReportsFollowUps,
        Permissions.ReportsOpportunities,
        Permissions.ReportsPipeline,
        Permissions.ReportsConversion,
        Permissions.ReportsUserActivity,
        Permissions.ApiAccess
    };

    private static readonly HashSet<string> SalesExecutivePermissions = new(StringComparer.Ordinal)
    {
        Permissions.DashboardView,
        Permissions.CustomersView,
        Permissions.CustomersCreate,
        Permissions.CustomersEdit,
        Permissions.LeadsView,
        Permissions.LeadsCreate,
        Permissions.LeadsEdit,
        Permissions.LeadsConvert,
        Permissions.OpportunitiesView,
        Permissions.OpportunitiesCreate,
        Permissions.OpportunitiesEdit,
        Permissions.FollowUpsView,
        Permissions.FollowUpsCreate,
        Permissions.FollowUpsEdit,
        Permissions.FollowUpsDelete,
        Permissions.ActivitiesView,
        Permissions.ActivitiesCreate,
        Permissions.ActivitiesEdit,
        Permissions.ActivitiesDelete,
        Permissions.ReportsCustomers,
        Permissions.ReportsLeads,
        Permissions.ReportsFollowUps,
        Permissions.ReportsOpportunities,
        Permissions.ReportsPipeline,
        Permissions.ReportsConversion,
        Permissions.ApiAccess
    };

    private static readonly Dictionary<string, IReadOnlySet<string>> Map =
        new(StringComparer.OrdinalIgnoreCase)
        {
            [Roles.Admin] = AdminPermissions,
            [Roles.Manager] = ManagerPermissions,
            [Roles.SalesExecutive] = SalesExecutivePermissions
        };

    private static readonly IReadOnlySet<string> EmptySet = new HashSet<string>();

    public static IReadOnlySet<string> For(string role)
    {
        if (Map.TryGetValue(role, out var permissions))
        {
            return permissions;
        }

        return EmptySet;
    }

    public static bool Has(string role, string permission)
    {
        return For(role).Contains(permission);
    }
}

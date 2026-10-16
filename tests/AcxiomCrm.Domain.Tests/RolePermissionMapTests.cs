using AcxiomCrm.Domain.Security;
using Shouldly;

namespace AcxiomCrm.Domain.Tests;

public class RolePermissionMapTests
{
    [Theory]
    // SalesExecutive specific checks
    [InlineData(Roles.SalesExecutive, Permissions.LeadsAssign, false)]
    [InlineData(Roles.SalesExecutive, Permissions.CustomersDelete, false)]
    [InlineData(Roles.SalesExecutive, Permissions.LeadsDelete, false)]
    [InlineData(Roles.SalesExecutive, Permissions.OpportunitiesDelete, false)]
    [InlineData(Roles.SalesExecutive, Permissions.OpportunitiesReopen, false)]
    [InlineData(Roles.SalesExecutive, Permissions.FollowUpsHistorical, false)]
    [InlineData(Roles.SalesExecutive, Permissions.UsersView, false)]
    [InlineData(Roles.SalesExecutive, Permissions.ReportsUserActivity, false)]
    [InlineData(Roles.SalesExecutive, Permissions.AuditView, false)]
    [InlineData(Roles.SalesExecutive, Permissions.AuditViewTeam, false)]
    [InlineData(Roles.SalesExecutive, Permissions.DashboardView, true)]
    [InlineData(Roles.SalesExecutive, Permissions.CustomersView, true)]
    [InlineData(Roles.SalesExecutive, Permissions.CustomersCreate, true)]
    [InlineData(Roles.SalesExecutive, Permissions.CustomersEdit, true)]
    [InlineData(Roles.SalesExecutive, Permissions.LeadsView, true)]
    [InlineData(Roles.SalesExecutive, Permissions.LeadsCreate, true)]
    [InlineData(Roles.SalesExecutive, Permissions.LeadsEdit, true)]
    [InlineData(Roles.SalesExecutive, Permissions.LeadsConvert, true)]
    [InlineData(Roles.SalesExecutive, Permissions.OpportunitiesView, true)]
    [InlineData(Roles.SalesExecutive, Permissions.OpportunitiesCreate, true)]
    [InlineData(Roles.SalesExecutive, Permissions.OpportunitiesEdit, true)]
    [InlineData(Roles.SalesExecutive, Permissions.FollowUpsView, true)]
    [InlineData(Roles.SalesExecutive, Permissions.FollowUpsCreate, true)]
    [InlineData(Roles.SalesExecutive, Permissions.FollowUpsEdit, true)]
    [InlineData(Roles.SalesExecutive, Permissions.FollowUpsDelete, true)]
    [InlineData(Roles.SalesExecutive, Permissions.ActivitiesView, true)]
    [InlineData(Roles.SalesExecutive, Permissions.ActivitiesCreate, true)]
    [InlineData(Roles.SalesExecutive, Permissions.ActivitiesEdit, true)]
    [InlineData(Roles.SalesExecutive, Permissions.ActivitiesDelete, true)]
    [InlineData(Roles.SalesExecutive, Permissions.ReportsCustomers, true)]
    [InlineData(Roles.SalesExecutive, Permissions.ReportsLeads, true)]
    [InlineData(Roles.SalesExecutive, Permissions.ReportsFollowUps, true)]
    [InlineData(Roles.SalesExecutive, Permissions.ReportsOpportunities, true)]
    [InlineData(Roles.SalesExecutive, Permissions.ReportsPipeline, true)]
    [InlineData(Roles.SalesExecutive, Permissions.ReportsConversion, true)]
    [InlineData(Roles.SalesExecutive, Permissions.ApiAccess, true)]

    // Manager specific checks
    [InlineData(Roles.Manager, Permissions.UsersManage, false)]
    [InlineData(Roles.Manager, Permissions.UsersUnlock, false)]
    [InlineData(Roles.Manager, Permissions.UsersResetPassword, false)]
    [InlineData(Roles.Manager, Permissions.RolesView, false)]
    [InlineData(Roles.Manager, Permissions.AuditView, false)]
    [InlineData(Roles.Manager, Permissions.AuditViewTeam, true)]
    [InlineData(Roles.Manager, Permissions.ReportsAudit, false)]
    [InlineData(Roles.Manager, Permissions.LeadsAssign, true)]
    [InlineData(Roles.Manager, Permissions.CustomersDelete, true)]
    [InlineData(Roles.Manager, Permissions.LeadsDelete, true)]
    [InlineData(Roles.Manager, Permissions.OpportunitiesDelete, true)]
    [InlineData(Roles.Manager, Permissions.OpportunitiesReopen, true)]
    [InlineData(Roles.Manager, Permissions.FollowUpsHistorical, true)]
    [InlineData(Roles.Manager, Permissions.UsersView, true)]
    [InlineData(Roles.Manager, Permissions.ReportsUserActivity, true)]
    public void Matches_security_document(string role, string permission, bool expected)
    {
        RolePermissionMap.Has(role, permission).ShouldBe(expected);
    }

    [Fact]
    public void Admin_has_every_permission()
    {
        foreach (var permission in Permissions.All)
        {
            RolePermissionMap.Has(Roles.Admin, permission).ShouldBeTrue(
                $"Admin should have permission '{permission}'");
        }
    }

    [Fact]
    public void Unknown_role_has_no_permissions()
    {
        RolePermissionMap.For("UnknownRole").ShouldBeEmpty();
        RolePermissionMap.Has("UnknownRole", Permissions.DashboardView).ShouldBeFalse();
    }
}

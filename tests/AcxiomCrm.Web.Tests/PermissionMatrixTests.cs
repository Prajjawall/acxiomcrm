using System.Security.Claims;
using AcxiomCrm.Domain.Security;
using AcxiomCrm.Infrastructure.Options;
using AcxiomCrm.Web.Authorization;
using Microsoft.AspNetCore.Authorization;
using Shouldly;

namespace AcxiomCrm.Web.Tests;

public class PermissionMatrixTests
{
    private static async Task<bool> EvaluateAsync(string role, string permission, bool managerAuditAccess = true)
    {
        var options = Microsoft.Extensions.Options.Options.Create(new AuditOptions { ManagerAccess = managerAuditAccess });
        var handler = new PermissionHandler(options);

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, "test-user-id"),
            new Claim(ClaimTypes.Role, role)
        };
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"));
        var requirement = new PermissionRequirement(permission);
        var context = new AuthorizationHandlerContext([requirement], principal, null);

        await handler.HandleAsync(context);
        return context.HasSucceeded;
    }

    [Theory]
    [InlineData(Roles.Admin, Permissions.CustomersView, true)]
    [InlineData(Roles.Admin, Permissions.AuditViewTeam, true)]
    [InlineData(Roles.Admin, Permissions.AuditView, true)]
    [InlineData(Roles.Admin, Permissions.UsersManage, true)]
    [InlineData(Roles.Manager, Permissions.CustomersView, true)]
    [InlineData(Roles.Manager, Permissions.AuditViewTeam, true)]
    [InlineData(Roles.Manager, Permissions.AuditView, false)]
    [InlineData(Roles.Manager, Permissions.UsersManage, false)]
    [InlineData(Roles.SalesExecutive, Permissions.CustomersView, true)]
    [InlineData(Roles.SalesExecutive, Permissions.CustomersDelete, false)]
    [InlineData(Roles.SalesExecutive, Permissions.AuditViewTeam, false)]
    [InlineData(Roles.SalesExecutive, Permissions.UsersView, false)]
    public async Task Role_gets_expected_status_for_permission(string role, string permission, bool expected)
    {
        var result = await EvaluateAsync(role, permission);
        result.ShouldBe(expected);
    }

    [Fact]
    public async Task Audit_team_permission_requires_config_flag_for_manager()
    {
        // When managerAuditAccess is false, Manager cannot access AuditViewTeam
        var denied = await EvaluateAsync(Roles.Manager, Permissions.AuditViewTeam, managerAuditAccess: false);
        denied.ShouldBeFalse();

        // When managerAuditAccess is true, Manager can access AuditViewTeam
        var allowed = await EvaluateAsync(Roles.Manager, Permissions.AuditViewTeam, managerAuditAccess: true);
        allowed.ShouldBeTrue();
    }

    [Fact]
    public async Task Unknown_role_fails_closed()
    {
        var result = await EvaluateAsync("Guest", Permissions.CustomersView);
        result.ShouldBeFalse();
    }
}

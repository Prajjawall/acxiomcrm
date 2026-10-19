using AcxiomCrm.Application.Users;
using AcxiomCrm.Domain.Security;
using AcxiomCrm.Web.Controllers;
using AcxiomCrm.Web.ViewModels.Users;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using Shouldly;

namespace AcxiomCrm.Web.Tests;

public class RolesPageTests
{
    private readonly IUserAdminService _userAdminService = Substitute.For<IUserAdminService>();

    [Fact]
    public async Task Index_renders_three_roles_and_complete_permission_matrix()
    {
        var roleSummaries = new List<RoleSummary>
        {
            new(Roles.Admin, "Administrator", 2, [.. RolePermissionMap.For(Roles.Admin)]),
            new(Roles.Manager, "Manager", 3, [.. RolePermissionMap.For(Roles.Manager)]),
            new(Roles.SalesExecutive, "Sales Executive", 5, [.. RolePermissionMap.For(Roles.SalesExecutive)])
        };

        _userAdminService.GetRolesAsync(Arg.Any<CancellationToken>())
            .Returns(roleSummaries);

        var controller = new RolesController(_userAdminService);
        var result = await controller.Index();

        var viewResult = result.ShouldBeOfType<ViewResult>();
        var model = viewResult.Model.ShouldBeOfType<RolesViewModel>();

        model.Roles.Count.ShouldBe(3);
        model.Roles.First(r => r.Name == Roles.Admin).MemberCount.ShouldBe(2);

        // Matrix check
        model.Matrix.Count.ShouldBe(Permissions.All.Count);

        foreach (var row in model.Matrix)
        {
            row.Admin.ShouldBe(RolePermissionMap.Has(Roles.Admin, row.Permission));
            row.Manager.ShouldBe(RolePermissionMap.Has(Roles.Manager, row.Permission));
            row.SalesExecutive.ShouldBe(RolePermissionMap.Has(Roles.SalesExecutive, row.Permission));
        }
    }
}

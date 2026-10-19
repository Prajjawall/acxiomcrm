using AcxiomCrm.Application.Common;
using AcxiomCrm.Application.Users;
using AcxiomCrm.Domain.Security;
using AcxiomCrm.Domain.Validation;
using AcxiomCrm.Web.Controllers;
using AcxiomCrm.Web.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using NSubstitute;
using Shouldly;

namespace AcxiomCrm.Web.Tests;

public class ManagerCannotAdministerTests
{
    private readonly IUserAdminService _userAdminService = Substitute.For<IUserAdminService>();
    private readonly IUserDirectory _userDirectory = Substitute.For<IUserDirectory>();
    private readonly IFlash _flash = Substitute.For<IFlash>();

    [Fact]
    public void Manager_role_has_only_UsersView_and_no_admin_permissions()
    {
        RolePermissionMap.Has(Roles.Manager, Permissions.UsersView).ShouldBeTrue();
        RolePermissionMap.Has(Roles.Manager, Permissions.UsersManage).ShouldBeFalse();
        RolePermissionMap.Has(Roles.Manager, Permissions.UsersUnlock).ShouldBeFalse();
        RolePermissionMap.Has(Roles.Manager, Permissions.UsersResetPassword).ShouldBeFalse();
        RolePermissionMap.Has(Roles.Manager, Permissions.RolesView).ShouldBeFalse();
    }

    [Fact]
    public void SalesExecutive_has_no_user_admin_permissions()
    {
        RolePermissionMap.Has(Roles.SalesExecutive, Permissions.UsersView).ShouldBeFalse();
        RolePermissionMap.Has(Roles.SalesExecutive, Permissions.UsersManage).ShouldBeFalse();
        RolePermissionMap.Has(Roles.SalesExecutive, Permissions.UsersUnlock).ShouldBeFalse();
        RolePermissionMap.Has(Roles.SalesExecutive, Permissions.UsersResetPassword).ShouldBeFalse();
        RolePermissionMap.Has(Roles.SalesExecutive, Permissions.RolesView).ShouldBeFalse();
    }

    [Fact]
    public async Task Details_returns_NotFound_when_manager_queries_non_team_user()
    {
        var httpContext = new DefaultHttpContext();
        var controller = new UsersController(_userAdminService, _userDirectory, _flash)
        {
            ControllerContext = new ControllerContext { HttpContext = httpContext },
            TempData = new TempDataDictionary(httpContext, Substitute.For<ITempDataProvider>())
        };

        // Service returns NotFound when user is outside manager's scope
        _userAdminService.GetAsync("other-team-user", Arg.Any<CancellationToken>())
            .Returns(Result<UserDetail>.Fail(ErrorKind.NotFound, new FieldError("Id", "User not found.")));

        var result = await controller.Details("other-team-user");
        result.ShouldBeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task Edit_returns_Forbid_when_service_returns_Forbidden()
    {
        var httpContext = new DefaultHttpContext();
        var controller = new UsersController(_userAdminService, _userDirectory, _flash)
        {
            ControllerContext = new ControllerContext { HttpContext = httpContext },
            TempData = new TempDataDictionary(httpContext, Substitute.For<ITempDataProvider>())
        };

        _userAdminService.GetAsync("user-1", Arg.Any<CancellationToken>())
            .Returns(Result<UserDetail>.Fail(ErrorKind.Forbidden));

        var result = await controller.Edit("user-1");
        result.ShouldBeOfType<ForbidResult>();
    }
}

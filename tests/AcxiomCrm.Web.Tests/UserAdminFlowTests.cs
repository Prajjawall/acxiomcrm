using AcxiomCrm.Application.Common;
using AcxiomCrm.Application.Users;
using AcxiomCrm.Domain.Security;
using AcxiomCrm.Web.Controllers;
using AcxiomCrm.Web.Services;
using AcxiomCrm.Web.ViewModels.Users;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using NSubstitute;
using Shouldly;

namespace AcxiomCrm.Web.Tests;

public class UserAdminFlowTests
{
    private readonly IUserAdminService _userAdminService = Substitute.For<IUserAdminService>();
    private readonly IUserDirectory _userDirectory = Substitute.For<IUserDirectory>();
    private readonly IFlash _flash = Substitute.For<IFlash>();

    private UsersController CreateController()
    {
        var httpContext = new DefaultHttpContext();
        return new UsersController(_userAdminService, _userDirectory, _flash)
        {
            ControllerContext = new ControllerContext { HttpContext = httpContext },
            TempData = new TempDataDictionary(httpContext, Substitute.For<ITempDataProvider>())
        };
    }

    [Fact]
    public async Task Admin_user_management_lifecycle_flow()
    {
        var controller = CreateController();

        // 1. Admin creates user
        _userAdminService.CreateAsync(Arg.Any<CreateUserCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result<CreatedUserResult>.Ok(new CreatedUserResult("new-user-1", "TempSecretP@ss16!")));

        var createResult = await controller.Create(new UserFormViewModel
        {
            FullName = "New Employee",
            UserName = "emp.new",
            Email = "emp.new@acxiom.com",
            Role = Roles.SalesExecutive,
            ManagerId = "mgr-1",
            IsActive = true
        });

        var createRedirect = createResult.ShouldBeOfType<RedirectToActionResult>();
        createRedirect.ActionName.ShouldBe(nameof(UsersController.Details));
        createRedirect.RouteValues!["id"].ShouldBe("new-user-1");

        // 2. Admin resets password
        _userAdminService.ResetPasswordAsync("new-user-1", Arg.Any<CancellationToken>())
            .Returns(Result<string>.Ok("ResetSecretP@ss18!"));

        var resetResult = await controller.ResetPassword("new-user-1");
        var resetRedirect = resetResult.ShouldBeOfType<RedirectToActionResult>();
        resetRedirect.ActionName.ShouldBe(nameof(UsersController.Details));
        _flash.Received().Success(Arg.Is<string>(s => s.Contains("ResetSecretP@ss18!")));

        // 3. Admin unlocks user
        _userAdminService.UnlockAsync("new-user-1", Arg.Any<CancellationToken>())
            .Returns(Result.Ok());

        var unlockResult = await controller.Unlock("new-user-1");
        var unlockRedirect = unlockResult.ShouldBeOfType<RedirectToActionResult>();
        unlockRedirect.ActionName.ShouldBe(nameof(UsersController.Details));

        // 4. Admin deactivates user
        _userAdminService.DeactivateAsync("new-user-1", Arg.Any<DeactivateUserCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Ok());

        var deactResult = await controller.Deactivate("new-user-1", "target-user-2");
        var deactRedirect = deactResult.ShouldBeOfType<RedirectToActionResult>();
        deactRedirect.ActionName.ShouldBe(nameof(UsersController.Details));
    }
}

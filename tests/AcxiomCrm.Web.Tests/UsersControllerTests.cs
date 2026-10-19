using AcxiomCrm.Application.Common;
using AcxiomCrm.Application.Users;
using AcxiomCrm.Domain.Security;
using AcxiomCrm.Domain.Validation;
using AcxiomCrm.Web.Controllers;
using AcxiomCrm.Web.Services;
using AcxiomCrm.Web.ViewModels.Users;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using NSubstitute;
using Shouldly;

namespace AcxiomCrm.Web.Tests;

public class UsersControllerTests
{
    private static readonly DateTime FixedDate = new(2026, 10, 19, 10, 0, 0, DateTimeKind.Utc);

    private readonly IUserAdminService _userAdminService = Substitute.For<IUserAdminService>();
    private readonly IUserDirectory _userDirectory = Substitute.For<IUserDirectory>();
    private readonly IFlash _flash = Substitute.For<IFlash>();

    public UsersControllerTests()
    {
        _userDirectory.ListAssignableOwnersAsync(Arg.Any<CancellationToken>())
            .Returns(new List<UserOption>
            {
                new("mgr-1", "Manager One", Roles.Manager),
                new("exec-1", "Exec One", Roles.SalesExecutive)
            });
    }

    private UsersController CreateController(string role = Roles.Admin, string userId = "admin-1")
    {
        var httpContext = new DefaultHttpContext();
        var tempData = new TempDataDictionary(httpContext, Substitute.For<ITempDataProvider>());

        return new UsersController(_userAdminService, _userDirectory, _flash)
        {
            ControllerContext = new ControllerContext { HttpContext = httpContext },
            TempData = tempData
        };
    }

    [Fact]
    public async Task Index_returns_view_with_users_and_filters()
    {
        var controller = CreateController();
        var items = new List<UserListItem>
        {
            new("u-1", "User One", "user1", "user1@acxiom.com", Roles.SalesExecutive, true, false, null, "mgr-1", "Manager One", null, FixedDate)
        };
        _userAdminService.SearchAsync(Arg.Any<UserFilter>(), Arg.Any<PageQuery>(), Arg.Any<CancellationToken>())
            .Returns(new PagedResult<UserListItem>(items, 1, 25, 1));

        var result = await controller.Index();

        var viewResult = result.ShouldBeOfType<ViewResult>();
        var model = viewResult.Model.ShouldBeOfType<UserListViewModel>();
        model.Result.Total.ShouldBe(1);
        model.Result.Items.Count.ShouldBe(1);
        model.ManagerOptions.Count.ShouldBe(1);
    }

    [Fact]
    public async Task Details_returns_not_found_when_user_does_not_exist()
    {
        var controller = CreateController();
        _userAdminService.GetAsync("unknown", Arg.Any<CancellationToken>())
            .Returns(Result<UserDetail>.Fail(ErrorKind.NotFound, new FieldError("Id", "User not found.")));

        var result = await controller.Details("unknown");

        result.ShouldBeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task Details_returns_view_when_user_exists()
    {
        var controller = CreateController();
        var detail = new UserDetail("u-1", "User One", "user1", "u1@acxiom.com", Roles.SalesExecutive, true, false, null, "mgr-1", "Manager One", false, null, FixedDate, 2, 1, 3);
        _userAdminService.GetAsync("u-1", Arg.Any<CancellationToken>())
            .Returns(Result<UserDetail>.Ok(detail));

        var result = await controller.Details("u-1");

        var viewResult = result.ShouldBeOfType<ViewResult>();
        var model = viewResult.Model.ShouldBeOfType<UserDetailsViewModel>();
        model.User.Id.ShouldBe("u-1");
        model.User.OpenLeads.ShouldBe(2);
        model.User.OpenOpportunities.ShouldBe(1);
        model.User.PlannedFollowUps.ShouldBe(3);
    }

    [Fact]
    public async Task Create_post_redirects_and_flashes_temp_password_on_success()
    {
        var controller = CreateController();
        var cmd = new CreateUserCommand("New User", "new.user", "new.user@acxiom.com", Roles.SalesExecutive, "mgr-1", true);
        _userAdminService.CreateAsync(Arg.Any<CreateUserCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result<CreatedUserResult>.Ok(new CreatedUserResult("new-id", "TempP@ssword12345!")));

        var form = new UserFormViewModel
        {
            FullName = "New User",
            UserName = "new.user",
            Email = "new.user@acxiom.com",
            Role = Roles.SalesExecutive,
            ManagerId = "mgr-1",
            IsActive = true
        };

        var result = await controller.Create(form);

        var redirect = result.ShouldBeOfType<RedirectToActionResult>();
        redirect.ActionName.ShouldBe(nameof(UsersController.Details));
        redirect.RouteValues!["id"].ShouldBe("new-id");
        _flash.Received(1).Success(Arg.Is<string>(s => s.Contains("TempP@ssword12345!")));
    }

    [Fact]
    public async Task Create_post_redisplays_view_when_service_returns_validation_failure()
    {
        var controller = CreateController();
        _userAdminService.CreateAsync(Arg.Any<CreateUserCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result<CreatedUserResult>.Fail(ErrorKind.Conflict, new FieldError("UserName", "Username is already taken.")));

        var form = new UserFormViewModel
        {
            FullName = "New User",
            UserName = "taken.user",
            Email = "new@acxiom.com",
            Role = Roles.SalesExecutive
        };

        var result = await controller.Create(form);

        var viewResult = result.ShouldBeOfType<ViewResult>();
        controller.ModelState.IsValid.ShouldBeFalse();
        controller.ModelState["UserName"]!.Errors.Count.ShouldBeGreaterThan(0);
    }

    [Fact]
    public async Task Edit_post_redirects_to_details_on_success()
    {
        var controller = CreateController();
        var detail = new UserDetail("u-1", "Updated User", "u1", "u1@acxiom.com", Roles.SalesExecutive, true, false, null, "mgr-1", "Manager One", false, null, FixedDate, 0, 0, 0);
        _userAdminService.UpdateAsync("u-1", Arg.Any<UpdateUserCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result<UserDetail>.Ok(detail));

        var form = new UserFormViewModel
        {
            Id = "u-1",
            FullName = "Updated User",
            UserName = "u1",
            Email = "u1@acxiom.com",
            Role = Roles.SalesExecutive,
            ManagerId = "mgr-1"
        };

        var result = await controller.Edit("u-1", form);

        var redirect = result.ShouldBeOfType<RedirectToActionResult>();
        redirect.ActionName.ShouldBe(nameof(UsersController.Details));
        redirect.RouteValues!["id"].ShouldBe("u-1");
        _flash.Received(1).Success(Arg.Is<string>(s => s.Contains("updated successfully")));
    }

    [Fact]
    public async Task Unlock_post_calls_service_and_redirects()
    {
        var controller = CreateController();
        _userAdminService.UnlockAsync("u-1", Arg.Any<CancellationToken>())
            .Returns(Result.Ok());

        var result = await controller.Unlock("u-1");

        var redirect = result.ShouldBeOfType<RedirectToActionResult>();
        redirect.ActionName.ShouldBe(nameof(UsersController.Details));
        _flash.Received(1).Success(Arg.Is<string>(s => s.Contains("unlocked")));
    }

    [Fact]
    public async Task ResetPassword_post_flashes_temporary_password()
    {
        var controller = CreateController();
        _userAdminService.ResetPasswordAsync("u-1", Arg.Any<CancellationToken>())
            .Returns(Result<string>.Ok("NewTempPassword123!"));

        var result = await controller.ResetPassword("u-1");

        var redirect = result.ShouldBeOfType<RedirectToActionResult>();
        redirect.ActionName.ShouldBe(nameof(UsersController.Details));
        _flash.Received(1).Success(Arg.Is<string>(s => s.Contains("NewTempPassword123!")));
    }
}

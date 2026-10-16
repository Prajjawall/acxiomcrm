using AcxiomCrm.Application.Auth;
using AcxiomCrm.Application.Common;
using AcxiomCrm.Domain.Validation;
using AcxiomCrm.Infrastructure.Options;
using AcxiomCrm.Web.Controllers;
using AcxiomCrm.Web.Services;
using AcxiomCrm.Web.ViewModels.Account;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using NSubstitute;
using Shouldly;

namespace AcxiomCrm.Web.Tests;

public class AccountFlowTests
{
    private readonly IAuthService _authService = Substitute.For<IAuthService>();
    private readonly ISessionSigner _sessionSigner = Substitute.For<ISessionSigner>();
    private readonly IUrlHelper _urlHelper = Substitute.For<IUrlHelper>();

    private AccountController CreateController(string mode = "Open")
    {
        var options = Microsoft.Extensions.Options.Options.Create(new RegistrationOptions { Mode = mode });
        var controller = new AccountController(_authService, _sessionSigner, options)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            },
            TempData = new TempDataDictionary(new DefaultHttpContext(), Substitute.For<ITempDataProvider>()),
            Url = _urlHelper
        };
        return controller;
    }

    [Fact]
    public async Task Login_with_valid_credentials_signs_in_and_redirects_to_dashboard()
    {
        var controller = CreateController();
        _authService.AuthenticateAsync(Arg.Any<LoginCommand>())
            .Returns(new AuthOutcome(LoginStatus.Succeeded, "user-123"));

        var model = new LoginViewModel { UserNameOrEmail = "john.doe", Password = "ValidPass!123" };
        var result = await controller.Login(model);

        var redirect = result.ShouldBeOfType<RedirectToActionResult>();
        redirect.ActionName.ShouldBe("Index");
        redirect.ControllerName.ShouldBe("Dashboard");

        await _sessionSigner.Received(1).SignInAsync("user-123", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Login_with_local_ReturnUrl_redirects_to_ReturnUrl()
    {
        var controller = CreateController();
        _authService.AuthenticateAsync(Arg.Any<LoginCommand>())
            .Returns(new AuthOutcome(LoginStatus.Succeeded, "user-123"));
        _urlHelper.IsLocalUrl("/Customers/Details/5").Returns(true);

        var model = new LoginViewModel { UserNameOrEmail = "john.doe", Password = "ValidPass!123", ReturnUrl = "/Customers/Details/5" };
        var result = await controller.Login(model);

        var redirect = result.ShouldBeOfType<RedirectResult>();
        redirect.Url.ShouldBe("/Customers/Details/5");
    }

    [Fact]
    public async Task Login_with_invalid_credentials_returns_view_with_error()
    {
        var controller = CreateController();
        _authService.AuthenticateAsync(Arg.Any<LoginCommand>())
            .Returns(new AuthOutcome(LoginStatus.InvalidCredentials));

        var model = new LoginViewModel { UserNameOrEmail = "john.doe", Password = "WrongPassword" };
        var result = await controller.Login(model);

        var viewResult = result.ShouldBeOfType<ViewResult>();
        viewResult.Model.ShouldBe(model);
        controller.ModelState.ErrorCount.ShouldBeGreaterThan(0);
        await _sessionSigner.DidNotReceive().SignInAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Login_locked_out_returns_view_with_lockout_message()
    {
        var controller = CreateController();
        _authService.AuthenticateAsync(Arg.Any<LoginCommand>())
            .Returns(new AuthOutcome(LoginStatus.LockedOut, "user-123"));

        var model = new LoginViewModel { UserNameOrEmail = "locked.user", Password = "AnyPassword" };
        var result = await controller.Login(model);

        var viewResult = result.ShouldBeOfType<ViewResult>();
        controller.ModelState[string.Empty]?.Errors.ShouldContain(e => e.ErrorMessage.Contains("locked out"));
    }

    [Fact]
    public async Task Login_MustChangePassword_redirects_to_ChangePassword()
    {
        var controller = CreateController();
        _authService.AuthenticateAsync(Arg.Any<LoginCommand>())
            .Returns(new AuthOutcome(LoginStatus.MustChangePassword, "user-123"));

        var model = new LoginViewModel { UserNameOrEmail = "mustchange.user", Password = "TempPassword" };
        var result = await controller.Login(model);

        var redirect = result.ShouldBeOfType<RedirectToActionResult>();
        redirect.ActionName.ShouldBe(nameof(AccountController.ChangePassword));
        await _sessionSigner.Received(1).SignInAsync("user-123", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Register_Open_mode_signs_in_and_redirects_to_dashboard()
    {
        var controller = CreateController("Open");
        _authService.RegisterAsync(Arg.Any<RegisterCommand>())
            .Returns(Result<string>.Ok("user-new-id"));

        var model = new RegisterViewModel
        {
            FullName = "New User",
            UserName = "new.user",
            Email = "new@example.com",
            Password = "ValidPass!123",
            ConfirmPassword = "ValidPass!123"
        };
        var result = await controller.Register(model);

        var redirect = result.ShouldBeOfType<RedirectToActionResult>();
        redirect.ActionName.ShouldBe("Index");
        redirect.ControllerName.ShouldBe("Dashboard");
        await _sessionSigner.Received(1).SignInAsync("user-new-id", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Register_ApprovalRequired_mode_renders_RegistrationPending_view()
    {
        var controller = CreateController("ApprovalRequired");
        _authService.RegisterAsync(Arg.Any<RegisterCommand>())
            .Returns(Result<string>.Ok("user-new-id"));

        var model = new RegisterViewModel
        {
            FullName = "Pending User",
            UserName = "pending.user",
            Email = "pending@example.com",
            Password = "ValidPass!123",
            ConfirmPassword = "ValidPass!123"
        };
        var result = await controller.Register(model);

        var view = result.ShouldBeOfType<ViewResult>();
        view.ViewName.ShouldBe("RegistrationPending");
        await _sessionSigner.DidNotReceive().SignInAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Register_Disabled_mode_returns_NotFound()
    {
        var controller = CreateController("Disabled");

        var model = new RegisterViewModel
        {
            FullName = "Disabled User",
            UserName = "disabled.user",
            Email = "disabled@example.com",
            Password = "ValidPass!123",
            ConfirmPassword = "ValidPass!123"
        };
        var getResult = controller.Register();
        getResult.ShouldBeOfType<NotFoundResult>();

        var postResult = await controller.Register(model);
        postResult.ShouldBeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task Logout_records_logout_and_redirects_to_login()
    {
        var controller = CreateController();

        var result = await controller.Logout();

        await _authService.Received(1).RecordLogoutAsync(Arg.Any<CancellationToken>());
        await _sessionSigner.Received(1).SignOutAsync();
        var redirect = result.ShouldBeOfType<RedirectToActionResult>();
        redirect.ActionName.ShouldBe(nameof(AccountController.Login));
    }

    [Fact]
    public async Task ChangePassword_success_redirects_to_dashboard()
    {
        var controller = CreateController();
        _authService.ChangePasswordAsync(Arg.Any<ChangePasswordCommand>())
            .Returns(Result.Ok());

        var model = new ChangePasswordViewModel
        {
            CurrentPassword = "OldPass!123",
            NewPassword = "NewPass!456",
            ConfirmNewPassword = "NewPass!456"
        };
        var result = await controller.ChangePassword(model);

        var redirect = result.ShouldBeOfType<RedirectToActionResult>();
        redirect.ActionName.ShouldBe("Index");
        redirect.ControllerName.ShouldBe("Dashboard");
    }

    [Fact]
    public async Task ChangePassword_failure_adds_model_errors()
    {
        var controller = CreateController();
        _authService.ChangePasswordAsync(Arg.Any<ChangePasswordCommand>())
            .Returns(Result.Fail(ErrorKind.Validation, new FieldError("CurrentPassword", "Incorrect current password.")));

        var model = new ChangePasswordViewModel
        {
            CurrentPassword = "WrongOldPass",
            NewPassword = "NewPass!456",
            ConfirmNewPassword = "NewPass!456"
        };
        var result = await controller.ChangePassword(model);

        var view = result.ShouldBeOfType<ViewResult>();
        controller.ModelState["CurrentPassword"]?.Errors.ShouldContain(e => e.ErrorMessage == "Incorrect current password.");
    }
}

using AcxiomCrm.Application.Auth;
using AcxiomCrm.Application.Common;
using AcxiomCrm.Infrastructure.Options;
using AcxiomCrm.Web.Services;
using AcxiomCrm.Web.ViewModels.Account;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace AcxiomCrm.Web.Controllers;

public class AccountController(
    IAuthService authService,
    ISessionSigner sessionSigner,
    IOptions<RegistrationOptions> registrationOptions) : Controller
{
    [HttpGet]
    [AllowAnonymous]
    public IActionResult Login(string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToAction("Index", "Dashboard");
        }

        return View(new LoginViewModel { ReturnUrl = returnUrl });
    }

    [HttpPost]
    [AllowAnonymous]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var outcome = await authService.AuthenticateAsync(new LoginCommand(model.UserNameOrEmail, model.Password));
        switch (outcome.Status)
        {
            case LoginStatus.Succeeded:
                await sessionSigner.SignInAsync(outcome.UserId!);
                if (!string.IsNullOrEmpty(model.ReturnUrl) && Url.IsLocalUrl(model.ReturnUrl))
                {
                    return Redirect(model.ReturnUrl);
                }
                return RedirectToAction("Index", "Dashboard");

            case LoginStatus.MustChangePassword:
                await sessionSigner.SignInAsync(outcome.UserId!);
                return RedirectToAction(nameof(ChangePassword));

            case LoginStatus.LockedOut:
                ModelState.AddModelError(string.Empty, "Account is locked out. Please try again later.");
                return View(model);

            case LoginStatus.InvalidCredentials:
            default:
                ModelState.AddModelError(string.Empty, "Invalid username or password.");
                return View(model);
        }
    }

    [HttpGet]
    [AllowAnonymous]
    public IActionResult Register()
    {
        if (string.Equals(registrationOptions.Value.Mode, "Disabled", StringComparison.OrdinalIgnoreCase))
        {
            return NotFound();
        }

        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToAction("Index", "Dashboard");
        }

        return View(new RegisterViewModel());
    }

    [HttpPost]
    [AllowAnonymous]
    public async Task<IActionResult> Register(RegisterViewModel model)
    {
        if (string.Equals(registrationOptions.Value.Mode, "Disabled", StringComparison.OrdinalIgnoreCase))
        {
            return NotFound();
        }

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var result = await authService.RegisterAsync(new RegisterCommand(
            model.FullName,
            model.UserName,
            model.Email,
            model.Password));

        if (!result.Succeeded)
        {
            if (result.Kind == ErrorKind.NotFound)
            {
                return NotFound();
            }

            foreach (var err in result.Errors)
            {
                ModelState.AddModelError(err.Field, err.Message);
            }

            return View(model);
        }

        if (string.Equals(registrationOptions.Value.Mode, "ApprovalRequired", StringComparison.OrdinalIgnoreCase))
        {
            return View("RegistrationPending");
        }

        await sessionSigner.SignInAsync(result.Value!);
        return RedirectToAction("Index", "Dashboard");
    }

    [HttpPost]
    [Authorize]
    public async Task<IActionResult> Logout()
    {
        await authService.RecordLogoutAsync();
        await sessionSigner.SignOutAsync();
        return RedirectToAction(nameof(Login));
    }

    [HttpGet]
    [Authorize]
    public IActionResult ChangePassword()
    {
        return View(new ChangePasswordViewModel());
    }

    [HttpPost]
    [Authorize]
    public async Task<IActionResult> ChangePassword(ChangePasswordViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var result = await authService.ChangePasswordAsync(new ChangePasswordCommand(
            model.CurrentPassword,
            model.NewPassword));

        if (!result.Succeeded)
        {
            foreach (var err in result.Errors)
            {
                ModelState.AddModelError(err.Field, err.Message);
            }

            return View(model);
        }

        TempData["SuccessMessage"] = "Password changed successfully.";
        return RedirectToAction("Index", "Dashboard");
    }

    [HttpGet]
    [AllowAnonymous]
    public IActionResult AccessDenied()
    {
        return View();
    }

    [HttpGet]
    [AllowAnonymous]
    public IActionResult RegistrationPending()
    {
        return View();
    }
}

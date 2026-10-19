using AcxiomCrm.Application.Common;
using AcxiomCrm.Application.Users;
using AcxiomCrm.Domain.Security;
using AcxiomCrm.Web.Mapping;
using AcxiomCrm.Web.Services;
using AcxiomCrm.Web.ViewModels.Users;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AcxiomCrm.Web.Controllers;

[Authorize(Permissions.UsersView)]
public sealed class UsersController : Controller
{
    private readonly IUserAdminService _userAdminService;
    private readonly IUserDirectory _userDirectory;
    private readonly IFlash _flash;

    public UsersController(
        IUserAdminService userAdminService,
        IUserDirectory userDirectory,
        IFlash flash)
    {
        _userAdminService = userAdminService;
        _userDirectory = userDirectory;
        _flash = flash;
    }

    [HttpGet]
    public async Task<IActionResult> Index(
        string? search = null,
        string? role = null,
        bool? active = null,
        bool? lockedOut = null,
        string? managerId = null,
        int page = 1,
        int pageSize = 25,
        CancellationToken ct = default)
    {
        var filter = new UserFilter(search, role, active, lockedOut, managerId);
        var pageQuery = new PageQuery(page, pageSize);

        var result = await _userAdminService.SearchAsync(filter, pageQuery, ct);
        var assignable = await _userDirectory.ListAssignableOwnersAsync(ct);
        var managerOptions = assignable.Where(u => u.Role == Roles.Manager).ToList();

        var model = new UserListViewModel
        {
            Result = result,
            Filter = filter,
            PageQuery = pageQuery,
            ManagerOptions = managerOptions
        };

        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> Details(string id, CancellationToken ct = default)
    {
        var res = await _userAdminService.GetAsync(id, ct);
        if (!res.Succeeded)
        {
            if (res.Kind == ErrorKind.NotFound)
            {
                return NotFound();
            }

            return Forbid();
        }

        var assignable = await _userDirectory.ListAssignableOwnersAsync(ct);
        var reassignTargets = assignable.Where(u => u.Id != id).ToList();

        var model = new UserDetailsViewModel
        {
            User = res.Value!,
            ReassignTargets = reassignTargets
        };

        return View(model);
    }

    [HttpGet]
    [Authorize(Permissions.UsersManage)]
    public async Task<IActionResult> Create(CancellationToken ct = default)
    {
        var assignable = await _userDirectory.ListAssignableOwnersAsync(ct);
        var managerOptions = assignable.Where(u => u.Role == Roles.Manager).ToList();

        var model = new UserFormViewModel
        {
            IsActive = true,
            Role = Roles.SalesExecutive,
            ManagerOptions = managerOptions
        };

        return View(model);
    }

    [HttpPost]
    [Authorize(Permissions.UsersManage)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(UserFormViewModel model, CancellationToken ct = default)
    {
        if (!ModelState.IsValid)
        {
            var assignable = await _userDirectory.ListAssignableOwnersAsync(ct);
            model.ManagerOptions = assignable.Where(u => u.Role == Roles.Manager).ToList();
            return View(model);
        }

        var res = await _userAdminService.CreateAsync(model.ToCreateCommand(), ct);
        if (!res.Succeeded)
        {
            ModelState.AddValidationErrors(res.Errors);
            var assignable = await _userDirectory.ListAssignableOwnersAsync(ct);
            model.ManagerOptions = assignable.Where(u => u.Role == Roles.Manager).ToList();
            return View(model);
        }

        _flash.Success($"User '{model.FullName}' created successfully. Temporary password: {res.Value!.TemporaryPassword} (Share this securely; it is shown once).");
        return RedirectToAction(nameof(Details), new { id = res.Value.UserId });
    }

    [HttpGet]
    [Authorize(Permissions.UsersManage)]
    public async Task<IActionResult> Edit(string id, CancellationToken ct = default)
    {
        var res = await _userAdminService.GetAsync(id, ct);
        if (!res.Succeeded)
        {
            if (res.Kind == ErrorKind.NotFound)
            {
                return NotFound();
            }

            return Forbid();
        }

        var assignable = await _userDirectory.ListAssignableOwnersAsync(ct);
        var managerOptions = assignable.Where(u => u.Role == Roles.Manager && u.Id != id).ToList();

        var model = res.Value!.ToFormViewModel(managerOptions);
        return View(model);
    }

    [HttpPost]
    [Authorize(Permissions.UsersManage)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(string id, UserFormViewModel model, CancellationToken ct = default)
    {
        if (!ModelState.IsValid)
        {
            var assignable = await _userDirectory.ListAssignableOwnersAsync(ct);
            model.ManagerOptions = assignable.Where(u => u.Role == Roles.Manager && u.Id != id).ToList();
            return View(model);
        }

        var res = await _userAdminService.UpdateAsync(id, model.ToUpdateCommand(), ct);
        if (!res.Succeeded)
        {
            ModelState.AddValidationErrors(res.Errors);
            var assignable = await _userDirectory.ListAssignableOwnersAsync(ct);
            model.ManagerOptions = assignable.Where(u => u.Role == Roles.Manager && u.Id != id).ToList();
            return View(model);
        }

        _flash.Success($"User '{model.FullName}' updated successfully.");
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [Authorize(Permissions.UsersManage)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Activate(string id, CancellationToken ct = default)
    {
        var res = await _userAdminService.ActivateAsync(id, ct);
        if (!res.Succeeded)
        {
            _flash.Error(res.Errors.Count > 0 ? res.Errors[0].Message : "Failed to activate user.");
        }
        else
        {
            _flash.Success("User activated successfully.");
        }

        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [Authorize(Permissions.UsersManage)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Deactivate(string id, string? reassignToUserId = null, CancellationToken ct = default)
    {
        var res = await _userAdminService.DeactivateAsync(id, new DeactivateUserCommand(reassignToUserId), ct);
        if (!res.Succeeded)
        {
            _flash.Error(res.Errors.Count > 0 ? res.Errors[0].Message : "Failed to deactivate user.");
        }
        else
        {
            _flash.Success("User deactivated successfully.");
        }

        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [Authorize(Permissions.UsersUnlock)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Unlock(string id, CancellationToken ct = default)
    {
        var res = await _userAdminService.UnlockAsync(id, ct);
        if (!res.Succeeded)
        {
            _flash.Error(res.Errors.Count > 0 ? res.Errors[0].Message : "Failed to unlock user.");
        }
        else
        {
            _flash.Success("User unlocked successfully.");
        }

        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [Authorize(Permissions.UsersResetPassword)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetPassword(string id, CancellationToken ct = default)
    {
        var res = await _userAdminService.ResetPasswordAsync(id, ct);
        if (!res.Succeeded)
        {
            _flash.Error(res.Errors.Count > 0 ? res.Errors[0].Message : "Failed to reset password.");
        }
        else
        {
            _flash.Success($"Password reset successfully. Temporary password: {res.Value!} (Share this securely; it is shown once).");
        }

        return RedirectToAction(nameof(Details), new { id });
    }
}

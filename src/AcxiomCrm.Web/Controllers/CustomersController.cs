using System.Globalization;
using AcxiomCrm.Application.Abstractions;
using AcxiomCrm.Application.Audit;
using AcxiomCrm.Application.Common;
using AcxiomCrm.Application.Customers;
using AcxiomCrm.Application.Users;
using AcxiomCrm.Domain.Enums;
using AcxiomCrm.Domain.Security;
using AcxiomCrm.Web.Mapping;
using AcxiomCrm.Web.Services;
using AcxiomCrm.Web.ViewModels.Customers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AcxiomCrm.Web.Controllers;

[Authorize(Policy = Permissions.CustomersView)]
public sealed class CustomersController : Controller
{
    private readonly ICustomerService _customerService;
    private readonly IUserDirectory _userDirectory;
    private readonly IAuditQueryService _auditQueryService;
    private readonly ICurrentUser _currentUser;
    private readonly IFlash _flash;

    public CustomersController(
        ICustomerService customerService,
        IUserDirectory userDirectory,
        IAuditQueryService auditQueryService,
        ICurrentUser currentUser,
        IFlash flash)
    {
        _customerService = customerService;
        _userDirectory = userDirectory;
        _auditQueryService = auditQueryService;
        _currentUser = currentUser;
        _flash = flash;
    }

    [HttpGet]
    public async Task<IActionResult> Index(
        string? search = null,
        CustomerStatus? status = null,
        string? ownerId = null,
        int page = 1,
        int pageSize = 20,
        string? sort = null,
        bool desc = false,
        CancellationToken ct = default)
    {
        var filter = new CustomerFilter(search, status, ownerId);
        var pageQuery = new PageQuery(page, pageSize, sort, desc);
        var result = await _customerService.SearchAsync(filter, pageQuery, ct);

        IReadOnlyList<UserOption> owners = [];
        if (_currentUser.Role is Roles.Admin or Roles.Manager)
        {
            owners = await _userDirectory.ListAssignableOwnersAsync(ct);
        }

        var model = new CustomerListViewModel
        {
            Filter = filter,
            PageQuery = pageQuery,
            Result = result,
            OwnerOptions = owners,
            CanCreate = _currentUser.Can(Permissions.CustomersCreate)
        };

        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> Details(int id, CancellationToken ct = default)
    {
        var result = await _customerService.GetAsync(id, ct);
        if (result.Kind == ErrorKind.NotFound)
        {
            return NotFound();
        }
        if (result.Kind == ErrorKind.Forbidden)
        {
            return Forbid();
        }

        var auditResult = await _auditQueryService.ForRecordAsync(
            "Customer",
            id.ToString(CultureInfo.InvariantCulture),
            new PageQuery(1, 50, "Timestamp", true),
            ct);

        var model = new CustomerDetailsViewModel
        {
            Customer = result.Value!,
            AuditHistory = auditResult.Items,
            CanEdit = _currentUser.Can(Permissions.CustomersEdit),
            CanDelete = _currentUser.Can(Permissions.CustomersDelete)
        };

        return View(model);
    }

    [HttpGet]
    [Authorize(Policy = Permissions.CustomersCreate)]
    public async Task<IActionResult> Create(CancellationToken ct = default)
    {
        IReadOnlyList<UserOption> owners = [];
        if (_currentUser.Role is Roles.Admin or Roles.Manager)
        {
            owners = await _userDirectory.ListAssignableOwnersAsync(ct);
        }

        var model = new CustomerFormViewModel
        {
            OwnerOptions = owners
        };

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = Permissions.CustomersCreate)]
    public async Task<IActionResult> Create(CustomerFormViewModel model, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(model);

        if (!ModelState.IsValid)
        {
            await PopulateOwnerOptionsAsync(model, ct);
            return View(model);
        }

        var result = await _customerService.CreateAsync(model.ToCreateCommand(), ct);
        if (result.Succeeded)
        {
            _flash.Success("Customer created.");
            return RedirectToAction(nameof(Details), new { id = result.Value!.Id });
        }

        if (result.Kind == ErrorKind.Forbidden)
        {
            return Forbid();
        }

        ModelState.ApplyErrors(result.Errors);
        await PopulateOwnerOptionsAsync(model, ct);
        return View(model);
    }

    [HttpGet]
    [Authorize(Policy = Permissions.CustomersEdit)]
    public async Task<IActionResult> Edit(int id, CancellationToken ct = default)
    {
        var result = await _customerService.GetAsync(id, ct);
        if (result.Kind == ErrorKind.NotFound)
        {
            return NotFound();
        }
        if (result.Kind == ErrorKind.Forbidden)
        {
            return Forbid();
        }

        IReadOnlyList<UserOption> owners = [];
        if (_currentUser.Role is Roles.Admin or Roles.Manager)
        {
            owners = await _userDirectory.ListAssignableOwnersAsync(ct);
        }

        var model = CustomerMapping.FromDetail(result.Value!, owners);
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = Permissions.CustomersEdit)]
    public async Task<IActionResult> Edit(int id, CustomerFormViewModel model, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(model);

        if (!ModelState.IsValid)
        {
            await PopulateOwnerOptionsAsync(model, ct);
            return View(model);
        }

        var result = await _customerService.UpdateAsync(id, model.ToUpdateCommand(), ct);
        if (result.Succeeded)
        {
            _flash.Success("Changes saved.");
            return RedirectToAction(nameof(Details), new { id });
        }

        if (result.Kind == ErrorKind.NotFound)
        {
            return NotFound();
        }
        if (result.Kind == ErrorKind.Forbidden)
        {
            return Forbid();
        }

        ModelState.ApplyErrors(result.Errors);
        await PopulateOwnerOptionsAsync(model, ct);
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = Permissions.CustomersDelete)]
    public async Task<IActionResult> Delete(int id, CancellationToken ct = default)
    {
        var result = await _customerService.DeleteAsync(id, ct);
        if (result.Kind == ErrorKind.NotFound)
        {
            return NotFound();
        }
        if (result.Kind == ErrorKind.Forbidden)
        {
            return Forbid();
        }

        _flash.Success("Customer deactivated.");
        return RedirectToAction(nameof(Index));
    }

    private async Task PopulateOwnerOptionsAsync(CustomerFormViewModel model, CancellationToken ct)
    {
        if (_currentUser.Role is Roles.Admin or Roles.Manager)
        {
            model.OwnerOptions = await _userDirectory.ListAssignableOwnersAsync(ct);
        }
    }
}

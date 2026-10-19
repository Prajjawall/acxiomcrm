using AcxiomCrm.Application.Abstractions;
using AcxiomCrm.Application.Audit;
using AcxiomCrm.Application.Common;
using AcxiomCrm.Application.Customers;
using AcxiomCrm.Application.Opportunities;
using AcxiomCrm.Application.Users;
using AcxiomCrm.Domain.Enums;
using AcxiomCrm.Domain.Security;
using AcxiomCrm.Web.Mapping;
using AcxiomCrm.Web.Services;
using AcxiomCrm.Web.ViewModels.Opportunities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AcxiomCrm.Web.Controllers;

[Authorize(Permissions.OpportunitiesView)]
public sealed class OpportunitiesController : Controller
{
    private readonly IOpportunityService _opportunityService;
    private readonly ICustomerService _customerService;
    private readonly IUserDirectory _userDirectory;
    private readonly IAuditQueryService _auditQueryService;
    private readonly IClock _clock;
    private readonly ICurrentUser _currentUser;
    private readonly IFlash _flash;

    public OpportunitiesController(
        IOpportunityService opportunityService,
        ICustomerService customerService,
        IUserDirectory userDirectory,
        IAuditQueryService auditQueryService,
        IClock clock,
        ICurrentUser currentUser,
        IFlash flash)
    {
        _opportunityService = opportunityService;
        _customerService = customerService;
        _userDirectory = userDirectory;
        _auditQueryService = auditQueryService;
        _clock = clock;
        _currentUser = currentUser;
        _flash = flash;
    }

    [HttpGet]
    public async Task<IActionResult> Index(
        string? view = "table",
        string? search = null,
        int? customerId = null,
        OpportunityStage? stage = null,
        OpportunityStatus? status = null,
        string? ownerId = null,
        int page = 1,
        int pageSize = 25,
        string? sort = null,
        bool desc = false,
        CancellationToken ct = default)
    {
        var filter = new OpportunityFilter(search, customerId, stage, status, ownerId);
        var pageQuery = new PageQuery(page, pageSize, sort, desc);

        var result = await _opportunityService.SearchAsync(filter, pageQuery, ct);
        var ownerOptions = await _userDirectory.ListAssignableOwnersAsync(ct);

        var model = new OpportunityListViewModel
        {
            Result = result,
            Filter = filter,
            PageQuery = pageQuery,
            OwnerOptions = ownerOptions,
            View = view ?? "table"
        };

        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> Details(int id, CancellationToken ct = default)
    {
        var result = await _opportunityService.GetAsync(id, ct);
        if (!result.Succeeded || result.Value is null)
        {
            return result.Kind == ErrorKind.Forbidden ? Forbid() : NotFound();
        }

        var detail = result.Value;
        var auditResult = await _auditQueryService.ForRecordAsync(
            "Opportunity",
            id.ToString(System.Globalization.CultureInfo.InvariantCulture),
            new PageQuery(1, 50, "Timestamp", true),
            ct);
        var history = auditResult.Items;

        var model = new OpportunityDetailsViewModel
        {
            Detail = detail,
            History = history,
            CanEdit = _currentUser.Can(Permissions.OpportunitiesEdit) && detail.Status == OpportunityStatus.Open,
            CanDelete = _currentUser.Can(Permissions.OpportunitiesDelete),
            CanReopen = _currentUser.Can(Permissions.OpportunitiesReopen) && detail.Status != OpportunityStatus.Open
        };

        return View(model);
    }

    [HttpGet]
    [Authorize(Permissions.OpportunitiesCreate)]
    public async Task<IActionResult> Create(int? customerId = null, CancellationToken ct = default)
    {
        var model = new OpportunityFormViewModel
        {
            ExpectedCloseDate = _clock.Today.AddDays(30),
            Stage = OpportunityStage.Qualification,
            Probability = 20
        };

        if (customerId.HasValue)
        {
            var custResult = await _customerService.GetAsync(customerId.Value, ct);
            if (custResult.Succeeded && custResult.Value is not null)
            {
                model.CustomerId = custResult.Value.Id;
                model.CustomerName = custResult.Value.Name;
            }
        }

        model.OwnerOptions = await _userDirectory.ListAssignableOwnersAsync(ct);
        return View(model);
    }

    [HttpPost]
    [Authorize(Permissions.OpportunitiesCreate)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(OpportunityFormViewModel model, CancellationToken ct = default)
    {
        if (!ModelState.IsValid)
        {
            await PopulateFormOptions(model, ct);
            return View(model);
        }

        var cmd = model.ToCreateCommand();
        var result = await _opportunityService.CreateAsync(cmd, ct);

        if (!result.Succeeded)
        {
            if (result.Kind == ErrorKind.Forbidden)
            {
                return Forbid();
            }

            ModelState.ApplyOpportunityErrors(result.Errors);
            await PopulateFormOptions(model, ct);
            return View(model);
        }

        _flash.Success($"Opportunity '{result.Value!.Name}' created successfully.");
        return RedirectToAction(nameof(Details), new { id = result.Value.Id });
    }

    [HttpGet]
    [Authorize(Permissions.OpportunitiesEdit)]
    public async Task<IActionResult> Edit(int id, CancellationToken ct = default)
    {
        var result = await _opportunityService.GetAsync(id, ct);
        if (!result.Succeeded || result.Value is null)
        {
            return result.Kind == ErrorKind.Forbidden ? Forbid() : NotFound();
        }

        var detail = result.Value;
        if (detail.Status != OpportunityStatus.Open)
        {
            _flash.Warning("This opportunity is closed and cannot be edited.");
            return RedirectToAction(nameof(Details), new { id });
        }

        var ownerOptions = await _userDirectory.ListAssignableOwnersAsync(ct);
        var model = OpportunityMapping.FromDetail(detail, ownerOptions);
        return View(model);
    }

    [HttpPost]
    [Authorize(Permissions.OpportunitiesEdit)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, OpportunityFormViewModel model, CancellationToken ct = default)
    {
        if (!ModelState.IsValid)
        {
            await PopulateFormOptions(model, ct);
            return View(model);
        }

        var cmd = model.ToUpdateCommand();
        var result = await _opportunityService.UpdateAsync(id, cmd, ct);

        if (!result.Succeeded)
        {
            if (result.Kind == ErrorKind.NotFound)
            {
                return NotFound();
            }
            if (result.Kind == ErrorKind.Forbidden)
            {
                return Forbid();
            }

            ModelState.ApplyOpportunityErrors(result.Errors);
            await PopulateFormOptions(model, ct);
            return View(model);
        }

        _flash.Success($"Opportunity '{result.Value!.Name}' updated successfully.");
        return RedirectToAction(nameof(Details), new { id = result.Value.Id });
    }

    [HttpPost]
    [Authorize(Permissions.OpportunitiesDelete)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id, CancellationToken ct = default)
    {
        var result = await _opportunityService.DeleteAsync(id, ct);
        if (!result.Succeeded)
        {
            return result.Kind switch
            {
                ErrorKind.NotFound => NotFound(),
                ErrorKind.Forbidden => Forbid(),
                _ => BadRequest(result.Errors)
            };
        }

        _flash.Success("Opportunity deleted successfully.");
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [Authorize(Permissions.OpportunitiesEdit)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangeStage(int id, ChangeStageViewModel model, CancellationToken ct = default)
    {
        byte[] rowVersion = ParseRowVersion(model.RowVersion);

        var result = await _opportunityService.ChangeStageAsync(id, model.TargetStage, model.CloseNote, rowVersion, ct);
        if (!result.Succeeded)
        {
            if (result.Kind == ErrorKind.NotFound)
            {
                return NotFound();
            }
            if (result.Kind == ErrorKind.Forbidden)
            {
                return Forbid();
            }

            ModelState.ApplyOpportunityErrors(result.Errors);
            var detailResult = await _opportunityService.GetAsync(id, ct);
            if (!detailResult.Succeeded || detailResult.Value is null)
            {
                return NotFound();
            }

            var auditResult = await _auditQueryService.ForRecordAsync(
                "Opportunity",
                id.ToString(System.Globalization.CultureInfo.InvariantCulture),
                new PageQuery(1, 50, "Timestamp", true),
                ct);

            var detailsVm = new OpportunityDetailsViewModel
            {
                Detail = detailResult.Value,
                History = auditResult?.Items ?? [],
                CanEdit = _currentUser.Can(Permissions.OpportunitiesEdit) && detailResult.Value.Status == OpportunityStatus.Open,
                CanDelete = _currentUser.Can(Permissions.OpportunitiesDelete),
                CanReopen = _currentUser.Can(Permissions.OpportunitiesReopen) && detailResult.Value.Status != OpportunityStatus.Open
            };

            return View(nameof(Details), detailsVm);
        }

        _flash.Success($"Opportunity moved to {result.Value!.Stage}.");
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [Authorize(Permissions.OpportunitiesReopen)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Reopen(int id, string rowVersion, CancellationToken ct = default)
    {
        byte[] bytes = ParseRowVersion(rowVersion);

        var result = await _opportunityService.ReopenAsync(id, bytes, ct);
        if (!result.Succeeded)
        {
            return result.Kind switch
            {
                ErrorKind.NotFound => NotFound(),
                ErrorKind.Forbidden => Forbid(),
                _ => RedirectToAction(nameof(Details), new { id })
            };
        }

        _flash.Success("Opportunity reopened successfully.");
        return RedirectToAction(nameof(Details), new { id });
    }

    private static byte[] ParseRowVersion(string? rowVersion)
    {
        if (string.IsNullOrWhiteSpace(rowVersion))
        {
            return [];
        }

        Span<byte> buffer = new byte[rowVersion.Length];
        if (Convert.TryFromBase64Chars(rowVersion, buffer, out int bytesWritten))
        {
            return buffer[..bytesWritten].ToArray();
        }

        return System.Text.Encoding.UTF8.GetBytes(rowVersion);
    }

    private async Task PopulateFormOptions(OpportunityFormViewModel model, CancellationToken ct)
    {
        var owners = await _userDirectory.ListAssignableOwnersAsync(ct);
        model.OwnerOptions = owners ?? [];
        if (model.CustomerId > 0 && string.IsNullOrEmpty(model.CustomerName))
        {
            var custResult = await _customerService.GetAsync(model.CustomerId, ct);
            if (custResult is { Succeeded: true, Value: not null })
            {
                model.CustomerName = custResult.Value.Name;
            }
        }
    }
}

using System.Globalization;
using AcxiomCrm.Application.Abstractions;
using AcxiomCrm.Application.Audit;
using AcxiomCrm.Application.Common;
using AcxiomCrm.Application.Customers;
using AcxiomCrm.Application.FollowUps;
using AcxiomCrm.Application.Leads;
using AcxiomCrm.Application.Opportunities;
using AcxiomCrm.Application.Users;
using AcxiomCrm.Domain.Enums;
using AcxiomCrm.Domain.Security;
using AcxiomCrm.Web.Mapping;
using AcxiomCrm.Web.Services;
using AcxiomCrm.Web.ViewModels.FollowUps;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AcxiomCrm.Web.Controllers;

[Authorize(Permissions.FollowUpsView)]
public sealed class FollowUpsController : Controller
{
    private readonly IFollowUpService _followUpService;
    private readonly ICustomerService _customerService;
    private readonly ILeadService _leadService;
    private readonly IOpportunityService _opportunityService;
    private readonly IUserDirectory _userDirectory;
    private readonly IAuditQueryService _auditQueryService;
    private readonly IClock _clock;
    private readonly ICurrentUser _currentUser;
    private readonly IFlash _flash;

    public FollowUpsController(
        IFollowUpService followUpService,
        ICustomerService customerService,
        ILeadService leadService,
        IOpportunityService opportunityService,
        IUserDirectory userDirectory,
        IAuditQueryService auditQueryService,
        IClock clock,
        ICurrentUser currentUser,
        IFlash flash)
    {
        _followUpService = followUpService;
        _customerService = customerService;
        _leadService = leadService;
        _opportunityService = opportunityService;
        _userDirectory = userDirectory;
        _auditQueryService = auditQueryService;
        _clock = clock;
        _currentUser = currentUser;
        _flash = flash;
    }

    [HttpGet]
    public async Task<IActionResult> Index(
        string? tab = "pending",
        FollowUpStatus? status = null,
        string? assignedToId = null,
        RelatedType? relatedType = null,
        int? relatedId = null,
        DateOnly? from = null,
        DateOnly? to = null,
        bool overdueOnly = false,
        bool dueTodayOnly = false,
        int page = 1,
        int pageSize = 25,
        CancellationToken ct = default)
    {
        RelatedRef? related = (relatedType.HasValue && relatedId.HasValue && relatedId.Value > 0)
            ? new RelatedRef(relatedType.Value, relatedId.Value)
            : null;

        var isPendingTab = string.Equals(tab, "pending", StringComparison.OrdinalIgnoreCase);

        var filter = new FollowUpFilter(
            Status: isPendingTab ? FollowUpStatus.Planned : status,
            AssignedToId: assignedToId,
            Related: related,
            From: from,
            To: to,
            OverdueOnly: isPendingTab && overdueOnly,
            DueTodayOnly: isPendingTab && dueTodayOnly);

        var pageQuery = new PageQuery(page, pageSize);
        var result = await _followUpService.SearchAsync(filter, pageQuery, ct);
        var assigneeOptions = await _userDirectory.ListAssignableOwnersAsync(ct);
        var counts = await _followUpService.GetNotificationCountsAsync(ct);

        var model = new FollowUpListViewModel
        {
            Result = result,
            Filter = filter,
            PageQuery = pageQuery,
            AssigneeOptions = assigneeOptions,
            Tab = tab ?? "pending",
            Counts = counts
        };

        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> Details(int id, CancellationToken ct = default)
    {
        var result = await _followUpService.GetAsync(id, ct);
        if (!result.Succeeded || result.Value is null)
        {
            return result.Kind == ErrorKind.Forbidden ? Forbid() : NotFound();
        }

        var detail = result.Value;
        var auditResult = await _auditQueryService.ForRecordAsync(
            "FollowUp",
            id.ToString(CultureInfo.InvariantCulture),
            new PageQuery(1, 50, "Timestamp", true),
            ct);

        var model = new FollowUpDetailsViewModel
        {
            Detail = detail,
            History = auditResult.Items,
            CanEdit = _currentUser.Can(Permissions.FollowUpsEdit) && detail.Status == FollowUpStatus.Planned,
            CanDelete = _currentUser.Can(Permissions.FollowUpsDelete) &&
                        (_currentUser.Role != Roles.SalesExecutive ||
                         (detail.AssignedToId == _currentUser.UserId && detail.Status == FollowUpStatus.Planned)),
            CanComplete = _currentUser.Can(Permissions.FollowUpsEdit) && detail.Status == FollowUpStatus.Planned
        };

        return View(model);
    }

    [HttpGet]
    [Authorize(Permissions.FollowUpsCreate)]
    public async Task<IActionResult> Create(RelatedType? relatedType = null, int? relatedId = null, CancellationToken ct = default)
    {
        var model = new FollowUpFormViewModel
        {
            FollowUpDate = _clock.Today,
            FollowUpTime = new TimeOnly(10, 0),
            AssigneeOptions = await _userDirectory.ListAssignableOwnersAsync(ct),
            AssignedToId = _currentUser.UserId
        };

        if (relatedType.HasValue && relatedId.HasValue && relatedId.Value > 0)
        {
            model.RelatedType = relatedType.Value;
            model.RelatedId = relatedId.Value;
            model.RelatedName = await LookupRelatedNameAsync(relatedType.Value, relatedId.Value, ct);
        }

        return View(model);
    }

    [HttpPost]
    [Authorize(Permissions.FollowUpsCreate)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(FollowUpFormViewModel model, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(model);

        if (!ModelState.IsValid)
        {
            model.AssigneeOptions = await _userDirectory.ListAssignableOwnersAsync(ct);
            return View(model);
        }

        var cmd = model.ToCreateCommand(_clock);
        var result = await _followUpService.CreateAsync(cmd, ct);

        if (!result.Succeeded)
        {
            ModelState.ApplyFollowUpErrors(result.Errors);
            model.AssigneeOptions = await _userDirectory.ListAssignableOwnersAsync(ct);
            return View(model);
        }

        _flash.Success("Follow-up scheduled.");
        return RedirectToAction(nameof(Details), new { id = result.Value!.Id });
    }

    [HttpGet]
    [Authorize(Permissions.FollowUpsEdit)]
    public async Task<IActionResult> Edit(int id, CancellationToken ct = default)
    {
        var result = await _followUpService.GetAsync(id, ct);
        if (!result.Succeeded || result.Value is null)
        {
            return result.Kind == ErrorKind.Forbidden ? Forbid() : NotFound();
        }

        if (result.Value.Status != FollowUpStatus.Planned)
        {
            _flash.Error("Only planned follow-ups can be edited.");
            return RedirectToAction(nameof(Details), new { id });
        }

        var assigneeOptions = await _userDirectory.ListAssignableOwnersAsync(ct);
        var model = FollowUpMapping.FromDetail(result.Value, _clock, assigneeOptions);
        return View(model);
    }

    [HttpPost]
    [Authorize(Permissions.FollowUpsEdit)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, FollowUpFormViewModel model, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(model);

        if (!ModelState.IsValid)
        {
            model.AssigneeOptions = await _userDirectory.ListAssignableOwnersAsync(ct);
            return View(model);
        }

        var cmd = model.ToUpdateCommand(_clock);
        var result = await _followUpService.UpdateAsync(id, cmd, ct);

        if (!result.Succeeded)
        {
            if (result.Kind == ErrorKind.Conflict)
            {
                _flash.Error(result.Errors.Count > 0 ? result.Errors[0].Message : "This follow-up was changed by someone else. Reload and try again.");
                return RedirectToAction(nameof(Edit), new { id });
            }

            ModelState.ApplyFollowUpErrors(result.Errors);
            model.AssigneeOptions = await _userDirectory.ListAssignableOwnersAsync(ct);
            return View(model);
        }

        _flash.Success("Follow-up updated.");
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [Authorize(Permissions.FollowUpsEdit)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Complete(int id, CompleteFollowUpViewModel model, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(model);

        byte[] rowVersion = string.IsNullOrEmpty(model.RowVersion)
            ? []
            : Convert.FromBase64String(model.RowVersion);

        var cmd = new CompleteFollowUpCommand(model.Remarks, model.LogActivity, rowVersion);
        var result = await _followUpService.CompleteAsync(id, cmd, ct);

        if (!result.Succeeded)
        {
            _flash.Error(result.Errors.Count > 0 ? result.Errors[0].Message : "Unable to complete follow-up.");
            return RedirectToAction(nameof(Details), new { id });
        }

        _flash.Success("Follow-up marked as completed.");

        if (model.ScheduleNext)
        {
            return RedirectToAction(nameof(Create), new
            {
                relatedType = result.Value!.RelatedType,
                relatedId = result.Value.RelatedId
            });
        }

        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [Authorize(Permissions.FollowUpsEdit)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Reschedule(int id, RescheduleFollowUpViewModel model, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(model);

        var newDate = model.NewDate ?? _clock.Today;
        var newTime = model.NewTime ?? new TimeOnly(10, 0);
        var newUtc = _clock.ToUtcFromBusinessLocal(newDate.ToDateTime(newTime));

        byte[] rowVersion = string.IsNullOrEmpty(model.RowVersion)
            ? []
            : Convert.FromBase64String(model.RowVersion);

        var result = await _followUpService.RescheduleAsync(id, newUtc, rowVersion, ct);

        if (!result.Succeeded)
        {
            _flash.Error(result.Errors.Count > 0 ? result.Errors[0].Message : "Unable to reschedule follow-up.");
            return RedirectToAction(nameof(Details), new { id });
        }

        _flash.Success("Follow-up rescheduled.");
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [Authorize(Permissions.FollowUpsEdit)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Miss(int id, string? rowVersion, CancellationToken ct = default)
    {
        byte[] bytes = string.IsNullOrEmpty(rowVersion) ? [] : Convert.FromBase64String(rowVersion);
        var result = await _followUpService.MarkMissedAsync(id, bytes, ct);

        if (!result.Succeeded)
        {
            _flash.Error(result.Errors.Count > 0 ? result.Errors[0].Message : "Unable to mark follow-up missed.");
            return RedirectToAction(nameof(Details), new { id });
        }

        _flash.Success("Follow-up marked as missed.");
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [Authorize(Permissions.FollowUpsEdit)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancel(int id, string? rowVersion, CancellationToken ct = default)
    {
        byte[] bytes = string.IsNullOrEmpty(rowVersion) ? [] : Convert.FromBase64String(rowVersion);
        var result = await _followUpService.CancelAsync(id, bytes, ct);

        if (!result.Succeeded)
        {
            _flash.Error(result.Errors.Count > 0 ? result.Errors[0].Message : "Unable to cancel follow-up.");
            return RedirectToAction(nameof(Details), new { id });
        }

        _flash.Success("Follow-up cancelled.");
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [Authorize(Permissions.FollowUpsDelete)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id, CancellationToken ct = default)
    {
        var result = await _followUpService.DeleteAsync(id, ct);
        if (!result.Succeeded)
        {
            _flash.Error(result.Errors.Count > 0 ? result.Errors[0].Message : "Unable to delete follow-up.");
            return RedirectToAction(nameof(Details), new { id });
        }

        _flash.Success("Follow-up deleted.");
        return RedirectToAction(nameof(Index));
    }

    private async Task<string?> LookupRelatedNameAsync(RelatedType type, int id, CancellationToken ct)
    {
        return type switch
        {
            RelatedType.Customer => (await _customerService.GetAsync(id, ct)).Value?.Name,
            RelatedType.Lead => (await _leadService.GetAsync(id, ct)).Value?.Name,
            RelatedType.Opportunity => (await _opportunityService.GetAsync(id, ct)).Value?.Name,
            _ => null
        };
    }
}

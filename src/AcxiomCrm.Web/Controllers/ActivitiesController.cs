using System.Globalization;
using AcxiomCrm.Application.Abstractions;
using AcxiomCrm.Application.Activities;
using AcxiomCrm.Application.Audit;
using AcxiomCrm.Application.Common;
using AcxiomCrm.Application.Customers;
using AcxiomCrm.Application.Leads;
using AcxiomCrm.Application.Opportunities;
using AcxiomCrm.Application.Users;
using AcxiomCrm.Domain.Enums;
using AcxiomCrm.Domain.Security;
using AcxiomCrm.Web.Mapping;
using AcxiomCrm.Web.Services;
using AcxiomCrm.Web.ViewModels.Activities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AcxiomCrm.Web.Controllers;

[Authorize(Permissions.ActivitiesView)]
public sealed class ActivitiesController : Controller
{
    private readonly IActivityService _activityService;
    private readonly ICustomerService _customerService;
    private readonly ILeadService _leadService;
    private readonly IOpportunityService _opportunityService;
    private readonly IUserDirectory _userDirectory;
    private readonly IAuditQueryService _auditQueryService;
    private readonly IClock _clock;
    private readonly ICurrentUser _currentUser;
    private readonly IFlash _flash;

    public ActivitiesController(
        IActivityService activityService,
        ICustomerService customerService,
        ILeadService leadService,
        IOpportunityService opportunityService,
        IUserDirectory userDirectory,
        IAuditQueryService auditQueryService,
        IClock clock,
        ICurrentUser currentUser,
        IFlash flash)
    {
        _activityService = activityService;
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
        ActivityType? type = null,
        ActivityStatus? status = null,
        string? assignedToId = null,
        RelatedType? relatedType = null,
        int? relatedId = null,
        DateOnly? from = null,
        DateOnly? to = null,
        int page = 1,
        int pageSize = 25,
        CancellationToken ct = default)
    {
        RelatedRef? related = (relatedType.HasValue && relatedId.HasValue && relatedId.Value > 0)
            ? new RelatedRef(relatedType.Value, relatedId.Value)
            : null;

        var filter = new ActivityFilter(
            Type: type,
            From: from,
            To: to,
            Status: status,
            AssignedToId: assignedToId,
            Related: related);

        var pageQuery = new PageQuery(page, pageSize);
        var result = await _activityService.SearchAsync(filter, pageQuery, ct);
        var assigneeOptions = await _userDirectory.ListAssignableOwnersAsync(ct);

        var model = new ActivityListViewModel
        {
            Result = result,
            Filter = filter,
            PageQuery = pageQuery,
            AssigneeOptions = assigneeOptions
        };

        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> Details(int id, CancellationToken ct = default)
    {
        var result = await _activityService.GetAsync(id, ct);
        if (!result.Succeeded || result.Value is null)
        {
            return result.Kind == ErrorKind.Forbidden ? Forbid() : NotFound();
        }

        var detail = result.Value;
        var auditResult = await _auditQueryService.ForRecordAsync(
            "Activity",
            id.ToString(CultureInfo.InvariantCulture),
            new PageQuery(1, 50, "Timestamp", true),
            ct);

        var model = new ActivityDetailsViewModel
        {
            Detail = detail,
            History = auditResult.Items,
            CanEdit = _currentUser.Can(Permissions.ActivitiesEdit),
            CanDelete = _currentUser.Can(Permissions.ActivitiesDelete)
        };

        return View(model);
    }

    [HttpGet]
    [Authorize(Permissions.ActivitiesCreate)]
    public async Task<IActionResult> Create(RelatedType? relatedType = null, int? relatedId = null, CancellationToken ct = default)
    {
        var model = new ActivityFormViewModel
        {
            ActivityDate = _clock.Today,
            ActivityTime = new TimeOnly(10, 0),
            AssigneeOptions = await _userDirectory.ListAssignableOwnersAsync(ct),
            AssignedToId = _currentUser.UserId
        };

        if (relatedType.HasValue && relatedId.HasValue && relatedId.Value > 0)
        {
            switch (relatedType.Value)
            {
                case RelatedType.Customer:
                    model.CustomerId = relatedId.Value;
                    model.CustomerName = (await _customerService.GetAsync(relatedId.Value, ct)).Value?.Name;
                    break;
                case RelatedType.Lead:
                    model.LeadId = relatedId.Value;
                    model.LeadName = (await _leadService.GetAsync(relatedId.Value, ct)).Value?.Name;
                    break;
                case RelatedType.Opportunity:
                    model.OpportunityId = relatedId.Value;
                    model.OpportunityName = (await _opportunityService.GetAsync(relatedId.Value, ct)).Value?.Name;
                    break;
            }
        }

        return View(model);
    }

    [HttpPost]
    [Authorize(Permissions.ActivitiesCreate)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(ActivityFormViewModel model, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(model);

        if (!ModelState.IsValid)
        {
            model.AssigneeOptions = await _userDirectory.ListAssignableOwnersAsync(ct);
            return View(model);
        }

        var cmd = model.ToCreateCommand(_clock);
        var result = await _activityService.CreateAsync(cmd, ct);

        if (!result.Succeeded)
        {
            ModelState.ApplyActivityErrors(result.Errors);
            model.AssigneeOptions = await _userDirectory.ListAssignableOwnersAsync(ct);
            return View(model);
        }

        _flash.Success("Activity logged.");
        return RedirectToAction(nameof(Details), new { id = result.Value!.Id });
    }

    [HttpGet]
    [Authorize(Permissions.ActivitiesEdit)]
    public async Task<IActionResult> Edit(int id, CancellationToken ct = default)
    {
        var result = await _activityService.GetAsync(id, ct);
        if (!result.Succeeded || result.Value is null)
        {
            return result.Kind == ErrorKind.Forbidden ? Forbid() : NotFound();
        }

        var assigneeOptions = await _userDirectory.ListAssignableOwnersAsync(ct);
        var model = ActivityMapping.FromDetail(result.Value, _clock, assigneeOptions);
        return View(model);
    }

    [HttpPost]
    [Authorize(Permissions.ActivitiesEdit)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, ActivityFormViewModel model, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(model);

        if (!ModelState.IsValid)
        {
            model.AssigneeOptions = await _userDirectory.ListAssignableOwnersAsync(ct);
            return View(model);
        }

        var cmd = model.ToUpdateCommand(_clock);
        var result = await _activityService.UpdateAsync(id, cmd, ct);

        if (!result.Succeeded)
        {
            if (result.Kind == ErrorKind.Conflict)
            {
                _flash.Error(result.Errors.Count > 0 ? result.Errors[0].Message : "This activity was changed by someone else. Reload and try again.");
                return RedirectToAction(nameof(Edit), new { id });
            }

            ModelState.ApplyActivityErrors(result.Errors);
            model.AssigneeOptions = await _userDirectory.ListAssignableOwnersAsync(ct);
            return View(model);
        }

        _flash.Success("Activity updated.");
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [Authorize(Permissions.ActivitiesDelete)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id, CancellationToken ct = default)
    {
        var result = await _activityService.DeleteAsync(id, ct);
        if (!result.Succeeded)
        {
            _flash.Error(result.Errors.Count > 0 ? result.Errors[0].Message : "Unable to delete activity.");
            return RedirectToAction(nameof(Details), new { id });
        }

        _flash.Success("Activity deleted.");
        return RedirectToAction(nameof(Index));
    }
}

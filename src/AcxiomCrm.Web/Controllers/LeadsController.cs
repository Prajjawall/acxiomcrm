using System.Globalization;
using AcxiomCrm.Application.Abstractions;
using AcxiomCrm.Application.Audit;
using AcxiomCrm.Application.Common;
using AcxiomCrm.Application.Customers;
using AcxiomCrm.Application.Leads;
using AcxiomCrm.Application.Users;
using AcxiomCrm.Domain.Enums;
using AcxiomCrm.Domain.Security;
using AcxiomCrm.Web.Mapping;
using AcxiomCrm.Web.Services;
using AcxiomCrm.Web.ViewModels.Leads;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AcxiomCrm.Web.Controllers;

[Authorize(Policy = Permissions.LeadsView)]
public sealed class LeadsController : Controller
{
    private readonly ILeadService _leadService;
    private readonly ICustomerService _customerService;
    private readonly IUserDirectory _userDirectory;
    private readonly IAuditQueryService _auditQueryService;
    private readonly IOpportunityFactory _opportunityFactory;
    private readonly IClock _clock;
    private readonly ICurrentUser _currentUser;
    private readonly IFlash _flash;

    public LeadsController(
        ILeadService leadService,
        ICustomerService customerService,
        IUserDirectory userDirectory,
        IAuditQueryService auditQueryService,
        IOpportunityFactory opportunityFactory,
        IClock clock,
        ICurrentUser currentUser,
        IFlash flash)
    {
        _leadService = leadService;
        _customerService = customerService;
        _userDirectory = userDirectory;
        _auditQueryService = auditQueryService;
        _opportunityFactory = opportunityFactory;
        _clock = clock;
        _currentUser = currentUser;
        _flash = flash;
    }

    [HttpGet]
    public async Task<IActionResult> Index(
        string? search = null,
        LeadStatus? status = null,
        LeadPriority? priority = null,
        LeadSource? source = null,
        string? ownerId = null,
        int page = 1,
        int pageSize = 20,
        string? sort = null,
        bool desc = false,
        CancellationToken ct = default)
    {
        var filter = new LeadFilter(search, status, priority, source, ownerId);
        var pageQuery = new PageQuery(page, pageSize, sort, desc);
        var result = await _leadService.SearchAsync(filter, pageQuery, ct);

        IReadOnlyList<UserOption> owners = [];
        if (_currentUser.Role is Roles.Admin or Roles.Manager)
        {
            owners = await _userDirectory.ListAssignableOwnersAsync(ct);
        }

        var model = new LeadListViewModel
        {
            Filter = filter,
            PageQuery = pageQuery,
            Result = result,
            OwnerOptions = owners,
            CanCreate = _currentUser.Can(Permissions.LeadsCreate)
        };

        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> Details(int id, CancellationToken ct = default)
    {
        var result = await _leadService.GetAsync(id, ct);
        if (result.Kind == ErrorKind.NotFound)
        {
            return NotFound();
        }
        if (result.Kind == ErrorKind.Forbidden)
        {
            return Forbid();
        }

        var auditResult = await _auditQueryService.ForRecordAsync(
            "Lead",
            id.ToString(CultureInfo.InvariantCulture),
            new PageQuery(1, 50, "Timestamp", true),
            ct);

        IReadOnlyList<UserOption> assignableOwners = [];
        if (_currentUser.Role is Roles.Admin or Roles.Manager)
        {
            assignableOwners = await _userDirectory.ListAssignableOwnersAsync(ct);
        }

        var lead = result.Value!;
        var model = new LeadDetailsViewModel
        {
            Lead = lead,
            AuditHistory = auditResult.Items,
            AssignableOwners = assignableOwners,
            CanEdit = _currentUser.Can(Permissions.LeadsEdit) && lead.Status is not (LeadStatus.Converted or LeadStatus.Lost),
            CanDelete = _currentUser.Can(Permissions.LeadsDelete),
            CanAssign = _currentUser.Can(Permissions.LeadsAssign) && lead.Status is not (LeadStatus.Converted or LeadStatus.Lost),
            CanConvert = _currentUser.Can(Permissions.LeadsConvert) && lead.Status == LeadStatus.Qualified
        };

        return View(model);
    }

    [HttpGet]
    [Authorize(Policy = Permissions.LeadsCreate)]
    public async Task<IActionResult> Create(CancellationToken ct = default)
    {
        IReadOnlyList<UserOption> owners = [];
        if (_currentUser.Role is Roles.Admin or Roles.Manager)
        {
            owners = await _userDirectory.ListAssignableOwnersAsync(ct);
        }

        var model = new LeadFormViewModel
        {
            OwnerOptions = owners
        };

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = Permissions.LeadsCreate)]
    public async Task<IActionResult> Create(LeadFormViewModel model, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(model);

        if (!ModelState.IsValid)
        {
            await PopulateOwnerOptionsAsync(model, ct);
            return View(model);
        }

        var result = await _leadService.CreateAsync(model.ToCreateCommand(), ct);
        if (result.Succeeded)
        {
            _flash.Success("Lead created.");
            return RedirectToAction(nameof(Details), new { id = result.Value!.Id });
        }

        if (result.Kind == ErrorKind.Forbidden)
        {
            return Forbid();
        }

        ModelState.ApplyLeadErrors(result.Errors);
        await PopulateOwnerOptionsAsync(model, ct);
        return View(model);
    }

    [HttpGet]
    [Authorize(Policy = Permissions.LeadsEdit)]
    public async Task<IActionResult> Edit(int id, CancellationToken ct = default)
    {
        var result = await _leadService.GetAsync(id, ct);
        if (result.Kind == ErrorKind.NotFound)
        {
            return NotFound();
        }
        if (result.Kind == ErrorKind.Forbidden)
        {
            return Forbid();
        }

        if (result.Value!.Status is LeadStatus.Converted or LeadStatus.Lost)
        {
            _flash.Error($"This lead is {result.Value.Status} and cannot be edited.");
            return RedirectToAction(nameof(Details), new { id });
        }

        IReadOnlyList<UserOption> owners = [];
        if (_currentUser.Role is Roles.Admin or Roles.Manager)
        {
            owners = await _userDirectory.ListAssignableOwnersAsync(ct);
        }

        var model = LeadMapping.FromDetail(result.Value, owners);
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = Permissions.LeadsEdit)]
    public async Task<IActionResult> Edit(int id, LeadFormViewModel model, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(model);

        if (!ModelState.IsValid)
        {
            await PopulateOwnerOptionsAsync(model, ct);
            return View(model);
        }

        byte[] rowVersion = string.IsNullOrEmpty(model.RowVersion)
            ? []
            : System.Convert.FromBase64String(model.RowVersion);

        var result = await _leadService.UpdateAsync(id, model.ToUpdateCommand(rowVersion), ct);
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

        ModelState.ApplyLeadErrors(result.Errors);
        await PopulateOwnerOptionsAsync(model, ct);
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = Permissions.LeadsDelete)]
    public async Task<IActionResult> Delete(int id, CancellationToken ct = default)
    {
        var result = await _leadService.DeleteAsync(id, ct);
        if (result.Kind == ErrorKind.NotFound)
        {
            return NotFound();
        }
        if (result.Kind == ErrorKind.Forbidden)
        {
            return Forbid();
        }

        _flash.Success("Lead deactivated.");
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = Permissions.LeadsEdit)]
    public async Task<IActionResult> ChangeStatus(int id, ChangeStatusViewModel model, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(model);

        var result = await _leadService.ChangeStatusAsync(id, model.To, model.RowVersion ?? [], ct);
        if (result.Kind == ErrorKind.NotFound)
        {
            return NotFound();
        }
        if (result.Kind == ErrorKind.Forbidden)
        {
            return Forbid();
        }

        if (result.Succeeded)
        {
            _flash.Success($"Lead status updated to {model.To}.");
        }
        else
        {
            _flash.Error(result.Errors.Count > 0 ? result.Errors[0].Message : "Unable to change status.");
        }

        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = Permissions.LeadsAssign)]
    public async Task<IActionResult> Assign(int id, AssignLeadViewModel model, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(model);

        var result = await _leadService.AssignAsync(id, model.OwnerId, model.RowVersion ?? [], ct);
        if (result.Kind == ErrorKind.NotFound)
        {
            return NotFound();
        }
        if (result.Kind == ErrorKind.Forbidden)
        {
            return Forbid();
        }

        if (result.Succeeded)
        {
            _flash.Success($"Lead assigned to {result.Value!.OwnerName}.");
        }
        else
        {
            _flash.Error(result.Errors.Count > 0 ? result.Errors[0].Message : "Unable to assign lead.");
        }

        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpGet]
    [Authorize(Policy = Permissions.LeadsConvert)]
    public async Task<IActionResult> Convert(int id, CancellationToken ct = default)
    {
        var result = await _leadService.GetAsync(id, ct);
        if (result.Kind == ErrorKind.NotFound)
        {
            return NotFound();
        }
        if (result.Kind == ErrorKind.Forbidden)
        {
            return Forbid();
        }

        var lead = result.Value!;
        if (lead.Status != LeadStatus.Qualified)
        {
            _flash.Error("Only qualified leads can be converted.");
            return RedirectToAction(nameof(Details), new { id });
        }

        var model = new ConvertLeadViewModel
        {
            LeadId = lead.Id,
            LeadCode = lead.Code,
            LeadName = lead.Name,
            LeadCompanyName = lead.CompanyName,
            LeadEmail = lead.Email,
            LeadPhone = lead.Phone,
            ExpectedValue = lead.ExpectedValue,
            OpportunityName = $"{lead.CompanyName ?? lead.Name} – Opportunity",
            OpportunityAmount = lead.ExpectedValue,
            OpportunityProbability = 20,
            OpportunityExpectedCloseDate = DateOnly.FromDateTime(_clock.UtcNow.AddDays(30)),
            RowVersion = lead.RowVersion,
            IsOpportunityAvailable = _opportunityFactory.IsAvailable
        };

        var suggestedId = await _leadService.FindConvertibleDuplicateCustomerIdAsync(id, ct);
        if (suggestedId.HasValue)
        {
            model.SuggestedCustomerId = suggestedId.Value;
            var customer = await _customerService.GetAsync(suggestedId.Value, ct);
            if (customer.Succeeded)
            {
                model.SuggestedCustomerLabel = string.IsNullOrWhiteSpace(customer.Value!.CompanyName)
                    ? $"{customer.Value.Name} ({customer.Value.Code})"
                    : $"{customer.Value.Name} – {customer.Value.CompanyName} ({customer.Value.Code})";
            }
        }

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = Permissions.LeadsConvert)]
    public async Task<IActionResult> Convert(int id, ConvertLeadViewModel model, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(model);

        var leadResult = await _leadService.GetAsync(id, ct);
        if (leadResult.Kind == ErrorKind.NotFound)
        {
            return NotFound();
        }
        if (leadResult.Kind == ErrorKind.Forbidden)
        {
            return Forbid();
        }

        var rowVersion = model.RowVersion ?? leadResult.Value!.RowVersion;
        var command = model.ToCommand(rowVersion);

        var result = await _leadService.ConvertAsync(id, command, ct);
        if (result.Succeeded)
        {
            var customerResult = await _customerService.GetAsync(result.Value!.CustomerId, ct);
            var customerCode = customerResult.Value?.Code ?? $"#{result.Value.CustomerId}";

            if (model.ExistingCustomerId.HasValue)
            {
                _flash.Success($"Lead converted. Linked to customer {customerCode}.");
            }
            else
            {
                _flash.Success($"Lead converted. Customer {customerCode} created.");
            }

            return RedirectToAction("Details", "Customers", new { id = result.Value.CustomerId });
        }

        if (result.Kind == ErrorKind.Forbidden)
        {
            return Forbid();
        }

        ModelState.ApplyLeadErrors(result.Errors);

        // Repopulate lead info and suggested duplicate if duplicate error
        model.LeadId = leadResult.Value!.Id;
        model.LeadCode = leadResult.Value.Code;
        model.LeadName = leadResult.Value.Name;
        model.LeadCompanyName = leadResult.Value.CompanyName;
        model.LeadEmail = leadResult.Value.Email;
        model.LeadPhone = leadResult.Value.Phone;
        model.ExpectedValue = leadResult.Value.ExpectedValue;
        model.RowVersion = leadResult.Value.RowVersion;
        model.IsOpportunityAvailable = _opportunityFactory.IsAvailable;

        var suggestedId = await _leadService.FindConvertibleDuplicateCustomerIdAsync(id, ct);
        if (suggestedId.HasValue)
        {
            model.SuggestedCustomerId = suggestedId.Value;
            var customer = await _customerService.GetAsync(suggestedId.Value, ct);
            if (customer.Succeeded)
            {
                model.SuggestedCustomerLabel = string.IsNullOrWhiteSpace(customer.Value!.CompanyName)
                    ? $"{customer.Value.Name} ({customer.Value.Code})"
                    : $"{customer.Value.Name} – {customer.Value.CompanyName} ({customer.Value.Code})";
            }
        }

        return View(model);
    }

    private async Task PopulateOwnerOptionsAsync(LeadFormViewModel model, CancellationToken ct)
    {
        if (_currentUser.Role is Roles.Admin or Roles.Manager)
        {
            model.OwnerOptions = await _userDirectory.ListAssignableOwnersAsync(ct);
        }
    }
}

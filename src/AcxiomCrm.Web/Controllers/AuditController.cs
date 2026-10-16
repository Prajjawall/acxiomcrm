using AcxiomCrm.Application.Audit;
using AcxiomCrm.Application.Common;
using AcxiomCrm.Domain.Enums;
using AcxiomCrm.Domain.Security;
using AcxiomCrm.Web.Presentation;
using AcxiomCrm.Web.ViewModels.Audit;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AcxiomCrm.Web.Controllers;

[Authorize(Policy = Permissions.AuditView)]
public class AuditController(IAuditQueryService auditQueryService) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(
        string? userId = null,
        string? module = null,
        string? actionName = null,
        AuditResult? result = null,
        DateOnly? from = null,
        DateOnly? to = null,
        string? entityName = null,
        string? recordId = null,
        string? text = null,
        int page = 1,
        int pageSize = 20)
    {
        if (from.HasValue && to.HasValue && from.Value > to.Value)
        {
            ModelState.AddModelError("From", "Start date must not be after end date.");
        }

        var filter = new AuditFilter(userId, module, actionName, result, from, to, entityName, recordId, text);
        var modules = await auditQueryService.ListModulesAsync();

        if (!ModelState.IsValid)
        {
            var emptyResult = new PagedResult<AuditListItem>([], page, pageSize, 0);
            return View(new AuditIndexViewModel
            {
                Filter = filter,
                Items = emptyResult,
                Pager = new PagerModel(page, pageSize, 0, nameof(Index)),
                Modules = modules
            });
        }

        var pagedItems = await auditQueryService.SearchAsync(filter, new PageQuery(page, pageSize));

        var query = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        if (!string.IsNullOrEmpty(userId)) query["userId"] = userId;
        if (!string.IsNullOrEmpty(module)) query["module"] = module;
        if (!string.IsNullOrEmpty(actionName)) query["actionName"] = actionName;
        if (result.HasValue) query["result"] = result.Value.ToString();
        if (from.HasValue) query["from"] = from.Value.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
        if (to.HasValue) query["to"] = to.Value.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
        if (!string.IsNullOrEmpty(entityName)) query["entityName"] = entityName;
        if (!string.IsNullOrEmpty(recordId)) query["recordId"] = recordId;
        if (!string.IsNullOrEmpty(text)) query["text"] = text;

        var pager = new PagerModel(pagedItems.Page, pagedItems.PageSize, pagedItems.Total, nameof(Index), query);

        var viewModel = new AuditIndexViewModel
        {
            Filter = filter,
            Items = pagedItems,
            Pager = pager,
            Modules = modules
        };

        return View(viewModel);
    }

    [HttpGet]
    public async Task<IActionResult> Details(long id)
    {
        var outcome = await auditQueryService.GetAsync(id);
        if (!outcome.Succeeded || outcome.Value == null)
        {
            return NotFound();
        }

        return View(new AuditDetailViewModel { Detail = outcome.Value });
    }
}

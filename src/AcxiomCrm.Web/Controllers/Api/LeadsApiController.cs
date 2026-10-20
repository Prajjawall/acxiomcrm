using AcxiomCrm.Application.Abstractions;
using AcxiomCrm.Application.Common;
using AcxiomCrm.Application.Leads;
using AcxiomCrm.Domain.Enums;
using AcxiomCrm.Domain.Security;
using AcxiomCrm.Web.Controllers.Api.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AcxiomCrm.Web.Controllers.Api;

[Authorize(Policy = Permissions.LeadsView)]
public class LeadsApiController : ApiControllerBase
{
    private readonly ILeadService _leadService;
    private readonly ICurrentUser _currentUser;

    public LeadsApiController(ILeadService leadService, ICurrentUser currentUser)
    {
        _leadService = leadService;
        _currentUser = currentUser;
    }

    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] string? search,
        [FromQuery] LeadStatus? status,
        [FromQuery] LeadPriority? priority,
        [FromQuery] LeadSource? source,
        [FromQuery] string? ownerId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        [FromQuery] string? sort = null,
        [FromQuery] bool desc = false,
        CancellationToken ct = default)
    {
        var filter = new LeadFilter(search, status, priority, source, ownerId);
        var pageQuery = new PageQuery(page, pageSize, sort, desc);
        var result = await _leadService.SearchAsync(filter, pageQuery, ct);

        var dtos = result.Items.Select(MapFromListItem).ToList();
        return Ok(new PagedDto<LeadDto>(dtos, result.Page, result.PageSize, result.Total));
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Get(int id, CancellationToken ct)
    {
        var result = await _leadService.GetAsync(id, ct);
        return ToActionResult(result, map: l => MapFromDetail(l));
    }

    [HttpPost]
    [Authorize(Policy = Permissions.LeadsCreate)]
    public async Task<IActionResult> Create([FromBody] CreateLeadRequest request, CancellationToken ct)
    {
        var ownerId = _currentUser.Role == Roles.SalesExecutive
            ? _currentUser.UserId
            : request.OwnerId;

        var command = new CreateLeadCommand(
            request.Name,
            request.CompanyName,
            request.Email,
            request.Phone,
            request.Source,
            request.Priority,
            request.ExpectedValue,
            ownerId,
            request.Notes);

        var result = await _leadService.CreateAsync(command, ct);
        return ToActionResult(
            result,
            map: l => MapFromDetail(l),
            location: l => $"/api/leads/{l.Id}");
    }

    private static LeadDto MapFromDetail(LeadDetail detail)
    {
        return new LeadDto(
            detail.Id,
            detail.Code,
            detail.Name,
            detail.CompanyName,
            detail.Email,
            detail.Phone,
            detail.Source,
            detail.Status,
            detail.Priority,
            detail.ExpectedValue,
            new OwnerSummaryDto(detail.OwnerId, detail.OwnerName),
            detail.Notes,
            detail.ConvertedCustomerId,
            detail.ConvertedOpportunityId,
            detail.CreatedDate);
    }

    private static LeadDto MapFromListItem(LeadListItem item)
    {
        return new LeadDto(
            item.Id,
            item.Code,
            item.Name,
            item.CompanyName,
            item.Email,
            item.Phone,
            item.Source,
            item.Status,
            item.Priority,
            item.ExpectedValue,
            new OwnerSummaryDto(item.OwnerId, item.OwnerName),
            null,
            null,
            null,
            item.CreatedDate);
    }
}

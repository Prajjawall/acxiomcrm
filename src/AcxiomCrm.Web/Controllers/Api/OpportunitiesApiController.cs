using AcxiomCrm.Application.Abstractions;
using AcxiomCrm.Application.Common;
using AcxiomCrm.Application.Opportunities;
using AcxiomCrm.Domain.Enums;
using AcxiomCrm.Domain.Security;
using AcxiomCrm.Web.Controllers.Api.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AcxiomCrm.Web.Controllers.Api;

[Authorize(Policy = Permissions.OpportunitiesView)]
public class OpportunitiesApiController : ApiControllerBase
{
    private readonly IOpportunityService _opportunityService;
    private readonly ICurrentUser _currentUser;

    public OpportunitiesApiController(IOpportunityService opportunityService, ICurrentUser currentUser)
    {
        _opportunityService = opportunityService;
        _currentUser = currentUser;
    }

    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] string? search,
        [FromQuery] int? customerId,
        [FromQuery] OpportunityStage? stage,
        [FromQuery] OpportunityStatus? status,
        [FromQuery] string? ownerId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        [FromQuery] string? sort = null,
        [FromQuery] bool desc = false,
        CancellationToken ct = default)
    {
        var filter = new OpportunityFilter(search, customerId, stage, status, ownerId);
        var pageQuery = new PageQuery(page, pageSize, sort, desc);
        var result = await _opportunityService.SearchAsync(filter, pageQuery, ct);

        var dtos = result.Items.Select(MapFromListItem).ToList();
        return Ok(new PagedDto<OpportunityDto>(dtos, result.Page, result.PageSize, result.Total));
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Get(int id, CancellationToken ct)
    {
        var result = await _opportunityService.GetAsync(id, ct);
        return ToActionResult(result, map: o => MapFromDetail(o));
    }

    [HttpPost]
    [Authorize(Policy = Permissions.OpportunitiesCreate)]
    public async Task<IActionResult> Create([FromBody] CreateOpportunityRequest request, CancellationToken ct)
    {
        var ownerId = _currentUser.Role == Roles.SalesExecutive
            ? _currentUser.UserId
            : request.OwnerId;

        var command = new CreateOpportunityCommand(
            request.Name,
            request.CustomerId,
            request.Amount,
            request.Probability,
            request.ExpectedCloseDate,
            request.Stage,
            request.Source,
            ownerId,
            request.Notes);

        var result = await _opportunityService.CreateAsync(command, ct);
        return ToActionResult(
            result,
            map: o => MapFromDetail(o),
            location: o => $"/api/opportunities/{o.Id}");
    }

    private static OpportunityDto MapFromDetail(OpportunityDetail detail)
    {
        return new OpportunityDto(
            detail.Id,
            detail.Name,
            new OpportunityCustomerSummaryDto(detail.CustomerId, detail.CustomerName, detail.CustomerCode),
            detail.LeadId,
            detail.Amount,
            detail.Probability,
            detail.WeightedAmount,
            detail.Stage,
            detail.Status,
            detail.ExpectedCloseDate,
            detail.ClosedDate,
            detail.CloseNote,
            detail.Source,
            new OwnerSummaryDto(detail.OwnerId, detail.OwnerName),
            detail.Notes,
            detail.CreatedDate);
    }

    private static OpportunityDto MapFromListItem(OpportunityListItem item)
    {
        return new OpportunityDto(
            item.Id,
            item.Name,
            new OpportunityCustomerSummaryDto(item.CustomerId, item.CustomerName, item.CustomerCode),
            null,
            item.Amount,
            item.Probability,
            item.WeightedAmount,
            item.Stage,
            item.Status,
            item.ExpectedCloseDate,
            null,
            null,
            null,
            new OwnerSummaryDto(item.OwnerId, item.OwnerName),
            null,
            null);
    }
}

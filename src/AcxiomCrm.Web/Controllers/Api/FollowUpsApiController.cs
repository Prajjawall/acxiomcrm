using AcxiomCrm.Application.Abstractions;
using AcxiomCrm.Application.Common;
using AcxiomCrm.Application.FollowUps;
using AcxiomCrm.Domain.Enums;
using AcxiomCrm.Domain.Security;
using AcxiomCrm.Web.Controllers.Api.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AcxiomCrm.Web.Controllers.Api;

[Authorize(Policy = Permissions.FollowUpsView)]
public class FollowUpsApiController : ApiControllerBase
{
    private readonly IFollowUpService _followUpService;
    private readonly ICurrentUser _currentUser;

    public FollowUpsApiController(IFollowUpService followUpService, ICurrentUser currentUser)
    {
        _followUpService = followUpService;
        _currentUser = currentUser;
    }

    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        [FromQuery] FollowUpStatus? status,
        [FromQuery] string? assignedToId,
        [FromQuery] int? customerId,
        [FromQuery] int? leadId,
        [FromQuery] int? opportunityId,
        [FromQuery] bool? overdue,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        [FromQuery] string? sort = null,
        [FromQuery] bool desc = false,
        CancellationToken ct = default)
    {
        RelatedRef? related = null;
        if (customerId.HasValue)
        {
            related = new RelatedRef(RelatedType.Customer, customerId.Value);
        }
        else if (leadId.HasValue)
        {
            related = new RelatedRef(RelatedType.Lead, leadId.Value);
        }
        else if (opportunityId.HasValue)
        {
            related = new RelatedRef(RelatedType.Opportunity, opportunityId.Value);
        }

        var filter = new FollowUpFilter(
            from,
            to,
            status,
            assignedToId,
            related,
            overdue ?? false,
            false);

        var pageQuery = new PageQuery(page, pageSize, sort, desc);
        var result = await _followUpService.SearchAsync(filter, pageQuery, ct);

        var dtos = result.Items.Select(MapFromListItem).ToList();
        return Ok(new PagedDto<FollowUpDto>(dtos, result.Page, result.PageSize, result.Total));
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Get(int id, CancellationToken ct)
    {
        var result = await _followUpService.GetAsync(id, ct);
        return ToActionResult(result, map: f => MapFromDetail(f));
    }

    [HttpPost]
    [Authorize(Policy = Permissions.FollowUpsCreate)]
    public async Task<IActionResult> Create([FromBody] CreateFollowUpRequest request, CancellationToken ct)
    {
        var assignedToId = _currentUser.Role == Roles.SalesExecutive
            ? _currentUser.UserId
            : (string.IsNullOrEmpty(request.AssignedToId) ? _currentUser.UserId : request.AssignedToId);

        var relatedId = request.RelatedType switch
        {
            RelatedType.Customer => request.CustomerId,
            RelatedType.Lead => request.LeadId,
            RelatedType.Opportunity => request.OpportunityId,
            _ => null
        };

        if (relatedId == null)
        {
            return BadRequest(ProblemFactory.CreateProblem(
                HttpContext,
                StatusCodes.Status400BadRequest,
                "Bad Request",
                "Related ID must be provided."));
        }

        var relatedRef = new RelatedRef(request.RelatedType, relatedId.Value);
        var command = new CreateFollowUpCommand(
            relatedRef,
            request.FollowUpDate,
            request.Subject,
            request.Type,
            request.Remarks,
            assignedToId);

        var result = await _followUpService.CreateAsync(command, ct);
        return ToActionResult(
            result,
            map: f => MapFromDetail(f),
            location: f => $"/api/followups/{f.Id}");
    }

    private static FollowUpDto MapFromDetail(FollowUpDetail detail)
    {
        return new FollowUpDto(
            detail.Id,
            detail.RelatedType,
            detail.RelatedId,
            detail.RelatedName,
            detail.FollowUpDateUtc,
            detail.Subject,
            detail.Type,
            detail.Status,
            detail.Remarks,
            new OwnerSummaryDto(detail.AssignedToId, detail.AssignedToName),
            detail.IsOverdue,
            detail.RescheduleCount,
            detail.CompletedDate);
    }

    private static FollowUpDto MapFromListItem(FollowUpListItem item)
    {
        return new FollowUpDto(
            item.Id,
            item.RelatedType,
            item.RelatedId,
            item.RelatedName,
            item.FollowUpDateUtc,
            item.Subject,
            item.Type,
            item.Status,
            null,
            new OwnerSummaryDto(item.AssignedToId, item.AssignedToName),
            item.IsOverdue,
            item.RescheduleCount,
            null);
    }
}

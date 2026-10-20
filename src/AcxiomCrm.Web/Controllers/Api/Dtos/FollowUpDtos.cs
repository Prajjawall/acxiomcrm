using AcxiomCrm.Domain.Enums;

namespace AcxiomCrm.Web.Controllers.Api.Dtos;

public sealed record FollowUpDto(
    int Id,
    RelatedType RelatedType,
    int RelatedId,
    string RelatedName,
    DateTime FollowUpDate,
    string Subject,
    FollowUpType Type,
    FollowUpStatus Status,
    string? Remarks,
    OwnerSummaryDto? AssignedTo,
    bool IsOverdue,
    int RescheduleCount,
    DateTime? CompletedDate);

public sealed record CreateFollowUpRequest(
    RelatedType RelatedType,
    int? CustomerId,
    int? LeadId,
    int? OpportunityId,
    DateTime FollowUpDate,
    string Subject,
    FollowUpType Type,
    string? AssignedToId,
    string? Remarks);

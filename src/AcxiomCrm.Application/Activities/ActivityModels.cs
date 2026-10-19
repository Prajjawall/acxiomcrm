using AcxiomCrm.Application.Common;
using AcxiomCrm.Domain.Enums;

namespace AcxiomCrm.Application.Activities;

public sealed record ActivityFilter(
    ActivityType? Type = null,
    DateOnly? From = null,
    DateOnly? To = null,
    ActivityStatus? Status = null,
    string? AssignedToId = null,
    RelatedRef? Related = null);

public sealed record ActivityListItem(
    int Id,
    ActivityType Type,
    string Subject,
    string? Description,
    DateTime ActivityDateUtc,
    IReadOnlyList<RelatedRef> Related,
    string AssignedToId,
    string AssignedToName,
    ActivityStatus Status);

public sealed record ActivityDetail(
    int Id,
    ActivityType Type,
    string Subject,
    string? Description,
    DateTime ActivityDateUtc,
    int? CustomerId,
    string? CustomerName,
    int? LeadId,
    string? LeadName,
    int? OpportunityId,
    string? OpportunityName,
    string AssignedToId,
    string AssignedToName,
    ActivityStatus Status,
    DateTime CreatedDate,
    byte[] RowVersion);

public sealed record CreateActivityCommand(
    ActivityType Type,
    string Subject,
    string? Description,
    DateTime ActivityDateUtc,
    IReadOnlyList<RelatedRef> Related,
    string? AssignedToId,
    ActivityStatus Status = ActivityStatus.Completed);

public sealed record UpdateActivityCommand(
    ActivityType Type,
    string Subject,
    string? Description,
    DateTime ActivityDateUtc,
    IReadOnlyList<RelatedRef> Related,
    string? AssignedToId,
    ActivityStatus Status,
    byte[] RowVersion);

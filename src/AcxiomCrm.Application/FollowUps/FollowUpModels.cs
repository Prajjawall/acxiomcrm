using AcxiomCrm.Application.Common;
using AcxiomCrm.Domain.Enums;

namespace AcxiomCrm.Application.FollowUps;

public sealed record FollowUpFilter(
    DateOnly? From = null,
    DateOnly? To = null,
    FollowUpStatus? Status = null,
    string? AssignedToId = null,
    RelatedRef? Related = null,
    bool OverdueOnly = false,
    bool DueTodayOnly = false);

public sealed record FollowUpListItem(
    int Id,
    RelatedType RelatedType,
    int RelatedId,
    string RelatedName,
    bool RelatedDeactivated,
    DateTime FollowUpDateUtc,
    string Subject,
    FollowUpType Type,
    FollowUpStatus Status,
    bool IsOverdue,
    bool IsDueToday,
    string AssignedToId,
    string AssignedToName,
    int RescheduleCount);

public sealed record FollowUpDetail(
    int Id,
    RelatedType RelatedType,
    int RelatedId,
    string RelatedName,
    DateTime FollowUpDateUtc,
    string Subject,
    FollowUpType Type,
    FollowUpStatus Status,
    string? Remarks,
    string AssignedToId,
    string AssignedToName,
    DateTime? CompletedDate,
    int RescheduleCount,
    bool IsHistoricalEntry,
    bool IsOverdue,
    DateTime CreatedDate,
    byte[] RowVersion);

public sealed record CreateFollowUpCommand(
    RelatedRef Related,
    DateTime FollowUpDateUtc,
    string Subject,
    FollowUpType Type,
    string? Remarks,
    string? AssignedToId,
    bool IsHistoricalEntry = false);

public sealed record UpdateFollowUpCommand(
    DateTime FollowUpDateUtc,
    string Subject,
    FollowUpType Type,
    string? Remarks,
    string? AssignedToId,
    byte[] RowVersion);

public sealed record CompleteFollowUpCommand(
    string? Remarks,
    bool LogActivity,
    byte[] RowVersion);

public sealed record NotificationCounts(int Overdue, int DueToday);

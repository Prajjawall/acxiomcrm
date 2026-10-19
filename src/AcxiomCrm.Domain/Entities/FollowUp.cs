using AcxiomCrm.Domain.Common;
using AcxiomCrm.Domain.Enums;

namespace AcxiomCrm.Domain.Entities;

public sealed class FollowUp : Entity, IOwnedEntity, ISoftDeletable, IAuditedEntity, IHasTimestamps
{
    public int? CustomerId { get; private set; }
    public Customer? Customer { get; private set; }

    public int? LeadId { get; private set; }
    public Lead? Lead { get; private set; }

    public int? OpportunityId { get; private set; }
    public Opportunity? Opportunity { get; private set; }

    public DateTime FollowUpDate { get; private set; }
    public string Subject { get; private set; } = string.Empty;
    public FollowUpType Type { get; private set; } = FollowUpType.Call;
    public FollowUpStatus Status { get; private set; } = FollowUpStatus.Planned;
    public string? Remarks { get; private set; }
    public string AssignedToId { get; private set; } = string.Empty;
    public DateTime? CompletedDate { get; private set; }
    public int RescheduleCount { get; private set; }
    public bool IsHistoricalEntry { get; private set; }

    public byte[] RowVersion { get; private set; } = [];

    // IOwnedEntity
    public string OwnerId => AssignedToId;

    // IHasTimestamps
    public DateTime CreatedDate { get; private set; }
    public string CreatedBy { get; private set; } = string.Empty;
    public DateTime? ModifiedDate { get; private set; }
    public string? ModifiedBy { get; private set; }

    // ISoftDeletable
    public bool IsDeleted { get; private set; }
    public DateTime? DeletedDate { get; private set; }
    public string? DeletedBy { get; private set; }

    public RelatedType RelatedType =>
        CustomerId.HasValue
            ? RelatedType.Customer
            : (LeadId.HasValue
                ? RelatedType.Lead
                : RelatedType.Opportunity);

    private FollowUp()
    {
    }

    public static FollowUp Create(
        int? customerId,
        int? leadId,
        int? opportunityId,
        DateTime followUpDate,
        string subject,
        FollowUpType type,
        string? remarks,
        string assignedToId,
        bool isHistoricalEntry)
    {
        return new FollowUp
        {
            CustomerId = customerId,
            LeadId = leadId,
            OpportunityId = opportunityId,
            FollowUpDate = followUpDate,
            Subject = subject,
            Type = type,
            Status = isHistoricalEntry ? FollowUpStatus.Completed : FollowUpStatus.Planned,
            Remarks = remarks,
            AssignedToId = assignedToId,
            IsHistoricalEntry = isHistoricalEntry,
            CompletedDate = isHistoricalEntry ? followUpDate : null,
            RescheduleCount = 0
        };
    }

    public void Complete(string? remarks, DateTime nowUtc)
    {
        if (Status != FollowUpStatus.Planned)
        {
            throw new InvalidOperationException($"Cannot complete a follow-up in status {Status}.");
        }

        Status = FollowUpStatus.Completed;
        CompletedDate = nowUtc;
        if (remarks is not null)
        {
            Remarks = remarks;
        }
    }

    public void Reschedule(DateTime newUtc)
    {
        if (Status != FollowUpStatus.Planned)
        {
            throw new InvalidOperationException($"Cannot reschedule a follow-up in status {Status}.");
        }

        FollowUpDate = newUtc;
        RescheduleCount++;
    }

    public void MarkMissed()
    {
        if (Status != FollowUpStatus.Planned)
        {
            throw new InvalidOperationException($"Cannot mark a follow-up missed in status {Status}.");
        }

        Status = FollowUpStatus.Missed;
    }

    public void Cancel()
    {
        if (Status != FollowUpStatus.Planned)
        {
            throw new InvalidOperationException($"Cannot cancel a follow-up in status {Status}.");
        }

        Status = FollowUpStatus.Cancelled;
    }

    public void SoftDelete(string deletedBy, DateTime deletedDate)
    {
        IsDeleted = true;
        DeletedBy = deletedBy;
        DeletedDate = deletedDate;
    }
}

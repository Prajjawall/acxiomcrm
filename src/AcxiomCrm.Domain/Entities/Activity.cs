using AcxiomCrm.Domain.Common;
using AcxiomCrm.Domain.Enums;

namespace AcxiomCrm.Domain.Entities;

public sealed class Activity : Entity, IOwnedEntity, ISoftDeletable, IAuditedEntity, IHasTimestamps
{
    public ActivityType Type { get; private set; } = ActivityType.Call;
    public string Subject { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public DateTime ActivityDate { get; private set; }

    public int? CustomerId { get; private set; }
    public Customer? Customer { get; private set; }

    public int? LeadId { get; private set; }
    public Lead? Lead { get; private set; }

    public int? OpportunityId { get; private set; }
    public Opportunity? Opportunity { get; private set; }

    public ActivityStatus Status { get; private set; } = ActivityStatus.Completed;

    public byte[] RowVersion { get; private set; } = [];

    // IOwnedEntity
    public string OwnerId { get; private set; } = string.Empty;
    public string AssignedToId => OwnerId;

    // IHasTimestamps
    public DateTime CreatedDate { get; private set; }
    public string CreatedBy { get; private set; } = string.Empty;
    public DateTime? ModifiedDate { get; private set; }
    public string? ModifiedBy { get; private set; }

    // ISoftDeletable
    public bool IsDeleted { get; private set; }
    public DateTime? DeletedDate { get; private set; }
    public string? DeletedBy { get; private set; }

    private Activity()
    {
    }

    public static Activity Create(
        ActivityType type,
        string subject,
        string? description,
        DateTime activityDate,
        int? customerId,
        int? leadId,
        int? opportunityId,
        string assignedToId,
        ActivityStatus status = ActivityStatus.Completed)
    {
        return new Activity
        {
            Type = type,
            Subject = subject,
            Description = description,
            ActivityDate = activityDate,
            CustomerId = customerId,
            LeadId = leadId,
            OpportunityId = opportunityId,
            OwnerId = assignedToId,
            Status = status
        };
    }

    public void Update(
        ActivityType type,
        string subject,
        string? description,
        DateTime activityDate,
        int? customerId,
        int? leadId,
        int? opportunityId,
        string assignedToId,
        ActivityStatus status)
    {
        Type = type;
        Subject = subject;
        Description = description;
        ActivityDate = activityDate;
        CustomerId = customerId;
        LeadId = leadId;
        OpportunityId = opportunityId;
        OwnerId = assignedToId;
        Status = status;
    }

    public void SoftDelete(string deletedBy, DateTime deletedDate)
    {
        IsDeleted = true;
        DeletedBy = deletedBy;
        DeletedDate = deletedDate;
    }
}

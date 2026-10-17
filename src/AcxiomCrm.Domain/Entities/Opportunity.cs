using AcxiomCrm.Domain.Common;
using AcxiomCrm.Domain.Enums;
using AcxiomCrm.Domain.Rules;

namespace AcxiomCrm.Domain.Entities;

public sealed class Opportunity : Entity, IOwnedEntity, ISoftDeletable, IAuditedEntity, IHasTimestamps
{
    public string Name { get; private set; } = string.Empty;
    public int CustomerId { get; private set; }
    public Customer? Customer { get; private set; }
    public int? LeadId { get; private set; }
    public Lead? Lead { get; private set; }
    public decimal Amount { get; private set; }
    public OpportunityStage Stage { get; private set; } = OpportunityStage.Qualification;
    public OpportunityStatus Status { get; private set; } = OpportunityStatus.Open;
    public byte Probability { get; private set; }
    public DateOnly ExpectedCloseDate { get; private set; }
    public DateOnly? ClosedDate { get; private set; }
    public string? CloseNote { get; private set; }
    public LeadSource? Source { get; private set; }
    public string OwnerId { get; private set; } = string.Empty;
    public string? Notes { get; private set; }

    public byte[] RowVersion { get; private set; } = [];

    // IHasTimestamps
    public DateTime CreatedDate { get; private set; }
    public string CreatedBy { get; private set; } = string.Empty;
    public DateTime? ModifiedDate { get; private set; }
    public string? ModifiedBy { get; private set; }

    // ISoftDeletable
    public bool IsDeleted { get; private set; }
    public DateTime? DeletedDate { get; private set; }
    public string? DeletedBy { get; private set; }

    private Opportunity()
    {
    }

    public static Opportunity Create(
        string name,
        int customerId,
        decimal amount,
        OpportunityStage stage,
        byte probability,
        DateOnly expectedCloseDate,
        string ownerId,
        int? leadId = null,
        LeadSource? source = null,
        string? notes = null,
        string? closeNote = null,
        DateOnly? closedDate = null)
    {
        var opp = new Opportunity
        {
            Name = name,
            CustomerId = customerId,
            LeadId = leadId,
            Amount = amount,
            Stage = stage,
            Status = OpportunityRules.StatusFor(stage),
            Probability = probability,
            ExpectedCloseDate = expectedCloseDate,
            OwnerId = ownerId,
            Source = source,
            Notes = notes
        };

        if (stage == OpportunityStage.Won)
        {
            if (string.IsNullOrWhiteSpace(closeNote))
            {
                throw new InvalidOperationException("Enter a note about why this opportunity was won.");
            }
            opp.Probability = 100;
            opp.ClosedDate = closedDate ?? expectedCloseDate;
            opp.CloseNote = closeNote;
        }
        else if (stage == OpportunityStage.Lost)
        {
            if (string.IsNullOrWhiteSpace(closeNote))
            {
                throw new InvalidOperationException("Enter the reason this opportunity was lost.");
            }
            opp.Probability = 0;
            opp.ClosedDate = closedDate ?? expectedCloseDate;
            opp.CloseNote = closeNote;
        }

        return opp;
    }

    public void UpdateDetails(
        string name,
        decimal amount,
        DateOnly expectedCloseDate,
        byte probability,
        LeadSource? source,
        string? notes)
    {
        if (Status != OpportunityStatus.Open)
        {
            throw new InvalidOperationException("This opportunity is closed and cannot be edited.");
        }

        Name = name;
        Amount = amount;
        ExpectedCloseDate = expectedCloseDate;
        Probability = probability;
        Source = source;
        Notes = notes;
    }

    public void MoveTo(OpportunityStage targetStage, DateOnly today, string? note)
    {
        if (Status != OpportunityStatus.Open)
        {
            throw new InvalidOperationException("This opportunity is closed and cannot be edited.");
        }

        if (targetStage == OpportunityStage.Won)
        {
            if (string.IsNullOrWhiteSpace(note))
            {
                throw new InvalidOperationException("Enter a note about why this opportunity was won.");
            }
            Stage = OpportunityStage.Won;
            Status = OpportunityStatus.Won;
            Probability = 100;
            ClosedDate = today;
            CloseNote = note;
        }
        else if (targetStage == OpportunityStage.Lost)
        {
            if (string.IsNullOrWhiteSpace(note))
            {
                throw new InvalidOperationException("Enter the reason this opportunity was lost.");
            }
            Stage = OpportunityStage.Lost;
            Status = OpportunityStatus.Lost;
            Probability = 0;
            ClosedDate = today;
            CloseNote = note;
        }
        else
        {
            Stage = targetStage;
            Status = OpportunityStatus.Open;
        }
    }

    public void Reopen()
    {
        Stage = OpportunityStage.Negotiation;
        Status = OpportunityStatus.Open;
        Probability = 75;
        ClosedDate = null;
        CloseNote = null;
    }

    public void AssignTo(string newOwnerId)
    {
        OwnerId = newOwnerId;
    }

    public void SoftDelete(string deletedBy, DateTime deletedDate)
    {
        IsDeleted = true;
        DeletedBy = deletedBy;
        DeletedDate = deletedDate;
    }
}

using AcxiomCrm.Domain.Common;
using AcxiomCrm.Domain.Enums;
using AcxiomCrm.Domain.Rules;
using AcxiomCrm.Domain.Validation;

namespace AcxiomCrm.Domain.Entities;

public sealed class Lead : Entity, IOwnedEntity, ISoftDeletable, IAuditedEntity, IHasTimestamps
{
    public string Code { get; private set; } = null!;
    public string Name { get; private set; } = string.Empty;
    public string? CompanyName { get; private set; }
    public string? Email { get; private set; }
    public string? EmailNormalized { get; private set; }
    public string? Phone { get; private set; }
    public LeadSource Source { get; private set; } = LeadSource.Website;
    public LeadStatus Status { get; private set; } = LeadStatus.New;
    public LeadPriority Priority { get; private set; } = LeadPriority.Medium;
    public decimal ExpectedValue { get; private set; }
    public string OwnerId { get; private set; } = string.Empty;
    public string? Notes { get; private set; }

    public int? ConvertedCustomerId { get; private set; }
    public Customer? ConvertedCustomer { get; private set; }
    public int? ConvertedOpportunityId { get; private set; }
    public DateTime? ConvertedDate { get; private set; }

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

    private Lead()
    {
    }

    public static Lead Create(
        string name,
        string? companyName,
        string? email,
        string? phone,
        LeadSource source,
        LeadPriority priority,
        decimal expectedValue,
        string ownerId,
        string? notes,
        LeadStatus status = LeadStatus.New)
    {
        if (status != LeadStatus.New)
        {
            throw new ArgumentException("A new lead must have status New.", nameof(status));
        }

        if (string.IsNullOrWhiteSpace(ownerId))
        {
            throw new ArgumentException("OwnerId is required.", nameof(ownerId));
        }

        var errors = LeadRules.Validate(name, companyName, email, phone, expectedValue, notes);
        if (errors.Count > 0)
        {
            throw new ArgumentException(errors[0].Message);
        }

        var lead = new Lead
        {
            Name = name.Trim(),
            CompanyName = string.IsNullOrWhiteSpace(companyName) ? null : companyName.Trim(),
            Email = string.IsNullOrWhiteSpace(email) ? null : email.Trim(),
            EmailNormalized = string.IsNullOrWhiteSpace(email) ? null : EmailRules.Normalize(email),
            Phone = string.IsNullOrWhiteSpace(phone) ? null : (PhoneRules.TryNormalize(phone, out var p) ? p : phone.Trim()),
            Source = source,
            Status = LeadStatus.New,
            Priority = priority,
            ExpectedValue = expectedValue,
            OwnerId = ownerId,
            Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim()
        };

        return lead;
    }

    public void UpdateDetails(
        string name,
        string? companyName,
        string? email,
        string? phone,
        LeadSource source,
        LeadPriority priority,
        decimal expectedValue,
        string? notes)
    {
        if (Status is LeadStatus.Converted or LeadStatus.Lost)
        {
            throw new InvalidOperationException($"This lead is {Status} and cannot be edited.");
        }

        var errors = LeadRules.Validate(name, companyName, email, phone, expectedValue, notes);
        if (errors.Count > 0)
        {
            throw new ArgumentException(errors[0].Message);
        }

        Name = name.Trim();
        CompanyName = string.IsNullOrWhiteSpace(companyName) ? null : companyName.Trim();
        Email = string.IsNullOrWhiteSpace(email) ? null : email.Trim();
        EmailNormalized = string.IsNullOrWhiteSpace(email) ? null : EmailRules.Normalize(email);
        Phone = string.IsNullOrWhiteSpace(phone) ? null : (PhoneRules.TryNormalize(phone, out var p) ? p : phone.Trim());
        Source = source;
        Priority = priority;
        ExpectedValue = expectedValue;
        Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
    }

    public void MoveTo(LeadStatus to)
    {
        if (to == LeadStatus.Converted)
        {
            throw new InvalidOperationException("A lead cannot move to Converted directly; conversion must be performed through MarkConverted.");
        }

        if (!LeadWorkflow.CanTransition(Status, to))
        {
            throw new InvalidOperationException($"A lead cannot move from {Status} to {to}.");
        }

        Status = to;
    }

    public void MarkConverted(int customerId, int? opportunityId, DateTime utcNow)
    {
        if (Status != LeadStatus.Qualified)
        {
            throw new InvalidOperationException("A lead must be Qualified before conversion.");
        }

        if (customerId <= 0)
        {
            throw new ArgumentException("A valid customer ID is required.", nameof(customerId));
        }

        Status = LeadStatus.Converted;
        ConvertedCustomerId = customerId;
        ConvertedOpportunityId = opportunityId;
        ConvertedDate = utcNow;
    }

    public void AssignTo(string newOwnerId)
    {
        if (string.IsNullOrWhiteSpace(newOwnerId))
        {
            throw new ArgumentException("OwnerId is required.", nameof(newOwnerId));
        }

        OwnerId = newOwnerId;
    }
}

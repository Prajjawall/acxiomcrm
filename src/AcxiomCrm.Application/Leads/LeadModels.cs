using AcxiomCrm.Domain.Enums;

namespace AcxiomCrm.Application.Leads;

public sealed record LeadFilter(
    string? Search = null,
    LeadStatus? Status = null,
    LeadPriority? Priority = null,
    LeadSource? Source = null,
    string? OwnerId = null);

public sealed record LeadListItem(
    int Id,
    string Code,
    string Name,
    string? CompanyName,
    string? Email,
    string? Phone,
    LeadSource Source,
    LeadStatus Status,
    LeadPriority Priority,
    decimal ExpectedValue,
    string OwnerId,
    string OwnerName,
    DateTime CreatedDate);

public sealed record LeadDetail(
    int Id,
    string Code,
    string Name,
    string? CompanyName,
    string? Email,
    string? Phone,
    LeadSource Source,
    LeadStatus Status,
    LeadPriority Priority,
    decimal ExpectedValue,
    string OwnerId,
    string OwnerName,
    string? Notes,
    int? ConvertedCustomerId,
    string? ConvertedCustomerName,
    int? ConvertedOpportunityId,
    DateTime? ConvertedDate,
    DateTime CreatedDate,
    DateTime? ModifiedDate,
    byte[] RowVersion,
    IReadOnlyList<LeadStatus> AllowedTransitions);

public sealed record CreateLeadCommand(
    string Name,
    string? CompanyName,
    string? Email,
    string? Phone,
    LeadSource Source,
    LeadPriority Priority,
    decimal ExpectedValue,
    string? OwnerId,
    string? Notes);

public sealed record UpdateLeadCommand(
    string Name,
    string? CompanyName,
    string? Email,
    string? Phone,
    LeadSource Source,
    LeadPriority Priority,
    decimal ExpectedValue,
    string? Notes,
    byte[] RowVersion);

public sealed record ConvertOpportunityInput(
    string Name,
    decimal Amount,
    byte Probability,
    DateOnly ExpectedCloseDate,
    string? Notes);

public sealed record ConvertLeadCommand(
    int? ExistingCustomerId,
    bool CreateOpportunity,
    ConvertOpportunityInput? Opportunity,
    byte[] RowVersion);

public sealed record ConvertLeadResult(
    int LeadId,
    int CustomerId,
    int? OpportunityId);

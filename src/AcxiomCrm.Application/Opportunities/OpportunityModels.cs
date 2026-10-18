using AcxiomCrm.Domain.Enums;

namespace AcxiomCrm.Application.Opportunities;

public sealed record OpportunityFilter(
    string? Search = null,
    int? CustomerId = null,
    OpportunityStage? Stage = null,
    OpportunityStatus? Status = null,
    string? OwnerId = null);

public sealed record OpportunityListItem(
    int Id,
    string Name,
    int CustomerId,
    string CustomerName,
    string CustomerCode,
    decimal Amount,
    int Probability,
    decimal WeightedAmount,
    OpportunityStage Stage,
    OpportunityStatus Status,
    DateOnly ExpectedCloseDate,
    bool CloseDateOverdue,
    string OwnerId,
    string OwnerName);

public sealed record OpportunityDetail(
    int Id,
    string Name,
    int CustomerId,
    string CustomerName,
    string CustomerCode,
    int? LeadId,
    decimal Amount,
    int Probability,
    decimal WeightedAmount,
    OpportunityStage Stage,
    OpportunityStatus Status,
    DateOnly ExpectedCloseDate,
    DateOnly? ClosedDate,
    string? CloseNote,
    LeadSource? Source,
    string OwnerId,
    string OwnerName,
    string? Notes,
    DateTime CreatedDate,
    DateTime? ModifiedDate,
    byte[] RowVersion);

public sealed record CreateOpportunityCommand(
    string Name,
    int CustomerId,
    decimal Amount,
    int Probability,
    DateOnly ExpectedCloseDate,
    OpportunityStage Stage,
    LeadSource? Source,
    string? OwnerId,
    string? Notes);

public sealed record UpdateOpportunityCommand(
    string Name,
    int CustomerId,
    decimal Amount,
    int Probability,
    DateOnly ExpectedCloseDate,
    LeadSource? Source,
    string? OwnerId,
    string? Notes,
    byte[] RowVersion);

public sealed record BoardColumn(
    OpportunityStage Stage,
    int Count,
    decimal Amount,
    decimal WeightedAmount,
    IReadOnlyList<OpportunityListItem> Cards);

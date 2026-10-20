using AcxiomCrm.Domain.Enums;

namespace AcxiomCrm.Web.Controllers.Api.Dtos;

public sealed record OpportunityCustomerSummaryDto(int Id, string Name, string Code);

public sealed record OpportunityDto(
    int Id,
    string Name,
    OpportunityCustomerSummaryDto Customer,
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
    OwnerSummaryDto? Owner,
    string? Notes,
    DateTime? CreatedDate);

public sealed record CreateOpportunityRequest(
    string Name,
    int CustomerId,
    decimal Amount,
    int Probability,
    DateOnly ExpectedCloseDate,
    OpportunityStage Stage,
    LeadSource? Source,
    string? OwnerId,
    string? Notes);

public sealed record UpdateOpportunityRequest(
    string Name,
    int CustomerId,
    decimal Amount,
    int Probability,
    DateOnly ExpectedCloseDate,
    LeadSource? Source,
    string? OwnerId,
    string? Notes);

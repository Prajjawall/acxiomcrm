using AcxiomCrm.Domain.Enums;

namespace AcxiomCrm.Web.Controllers.Api.Dtos;

public sealed record LeadDto(
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
    OwnerSummaryDto? Owner,
    string? Notes,
    int? ConvertedCustomerId,
    int? ConvertedOpportunityId,
    DateTime CreatedDate);

public sealed record CreateLeadRequest(
    string Name,
    string? CompanyName,
    string? Email,
    string? Phone,
    LeadSource Source,
    LeadPriority Priority,
    decimal ExpectedValue,
    string? OwnerId,
    string? Notes);

public sealed record UpdateLeadRequest(
    string Name,
    string? CompanyName,
    string? Email,
    string? Phone,
    LeadSource Source,
    LeadPriority Priority,
    decimal ExpectedValue,
    string? OwnerId,
    string? Notes);

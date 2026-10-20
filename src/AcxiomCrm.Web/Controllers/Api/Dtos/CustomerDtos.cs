using AcxiomCrm.Domain.Enums;

namespace AcxiomCrm.Web.Controllers.Api.Dtos;

public sealed record OwnerSummaryDto(string Id, string Name);

public sealed record CustomerDto(
    int Id,
    string Code,
    string Name,
    string? CompanyName,
    string Email,
    string Phone,
    string? Address,
    string? City,
    string? State,
    CustomerStatus Status,
    OwnerSummaryDto? Owner,
    string? Notes,
    DateTime CreatedDate,
    DateTime? ModifiedDate);

public sealed record CreateCustomerRequest(
    string Name,
    string? CompanyName,
    string Email,
    string Phone,
    string? Address,
    string? City,
    string? State,
    CustomerStatus Status,
    string? OwnerId,
    string? Notes);

public sealed record UpdateCustomerRequest(
    string Name,
    string? CompanyName,
    string Email,
    string Phone,
    string? Address,
    string? City,
    string? State,
    CustomerStatus Status,
    string? OwnerId,
    string? Notes);

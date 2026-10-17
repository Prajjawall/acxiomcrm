using AcxiomCrm.Domain.Enums;

namespace AcxiomCrm.Application.Customers;

public sealed record CustomerFilter(
    string? Search = null,
    CustomerStatus? Status = null,
    string? OwnerId = null);

public sealed record CustomerListItem(
    int Id,
    string Code,
    string Name,
    string? CompanyName,
    string Email,
    string Phone,
    CustomerStatus Status,
    string OwnerId,
    string OwnerName,
    DateTime CreatedDate);

public sealed record CustomerDetail(
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
    string OwnerId,
    string OwnerName,
    string? Notes,
    DateTime CreatedDate,
    DateTime? ModifiedDate,
    byte[] RowVersion);

public sealed record CreateCustomerCommand(
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

public sealed record UpdateCustomerCommand(
    string Name,
    string? CompanyName,
    string Email,
    string Phone,
    string? Address,
    string? City,
    string? State,
    CustomerStatus Status,
    string? OwnerId,
    string? Notes,
    byte[] RowVersion);

public sealed record CustomerLookupItem(
    int Id,
    string Code,
    string Name,
    string? CompanyName);

public sealed record CustomerDuplicate(string Field);

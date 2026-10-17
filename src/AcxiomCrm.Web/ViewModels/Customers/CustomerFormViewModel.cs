using AcxiomCrm.Application.Users;
using AcxiomCrm.Domain.Enums;
using AcxiomCrm.Domain.Validation;
using AcxiomCrm.Web.Validation;

namespace AcxiomCrm.Web.ViewModels.Customers;

public sealed class CustomerFormViewModel
{
    public int Id { get; set; }

    [AxRequired("Customer name")]
    [AxMaxLength(Limits.Name, "Customer name")]
    public string Name { get; set; } = string.Empty;

    [AxMaxLength(Limits.Name, "Company name")]
    public string? CompanyName { get; set; }

    [AxRequired("Email")]
    [AxMaxLength(Limits.Email, "Email")]
    [AxEmail]
    public string Email { get; set; } = string.Empty;

    [AxRequired("Phone number")]
    [AxPhone]
    public string Phone { get; set; } = string.Empty;

    [AxMaxLength(Limits.Address, "Address")]
    public string? Address { get; set; }

    [AxMaxLength(Limits.CityState, "City")]
    public string? City { get; set; }

    [AxMaxLength(Limits.CityState, "State")]
    public string? State { get; set; }

    public CustomerStatus Status { get; set; } = CustomerStatus.Active;

    public string? OwnerId { get; set; }

    [AxMaxLength(Limits.Notes, "Notes")]
    public string? Notes { get; set; }

    public string? RowVersion { get; set; }

    public IReadOnlyList<UserOption> OwnerOptions { get; set; } = [];
}

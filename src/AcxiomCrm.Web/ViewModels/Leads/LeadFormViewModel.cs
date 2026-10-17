using System.ComponentModel.DataAnnotations;
using AcxiomCrm.Application.Leads;
using AcxiomCrm.Application.Users;
using AcxiomCrm.Domain.Enums;
using AcxiomCrm.Domain.Validation;
using AcxiomCrm.Web.Validation;

namespace AcxiomCrm.Web.ViewModels.Leads;

public sealed class LeadFormViewModel : IValidatableObject
{
    public int Id { get; set; }

    public string? Code { get; set; }

    [AxRequired("Lead name")]
    [AxMaxLength(Limits.Name, "Lead name")]
    public string Name { get; set; } = string.Empty;

    [AxMaxLength(Limits.Name, "Company name")]
    public string? CompanyName { get; set; }

    [AxMaxLength(Limits.Email, "Email")]
    [AxEmail]
    public string? Email { get; set; }

    [AxPhone]
    public string? Phone { get; set; }

    public LeadSource Source { get; set; } = LeadSource.Website;

    public LeadPriority Priority { get; set; } = LeadPriority.Medium;

    [Range(typeof(decimal), "0", "100000000", ErrorMessage = "Expected value must be between 0 and 10,00,00,000.")]
    public decimal ExpectedValue { get; set; }

    public string? OwnerId { get; set; }

    [AxMaxLength(Limits.Notes, "Notes")]
    public string? Notes { get; set; }

    public string? RowVersion { get; set; }

    public IReadOnlyList<UserOption> OwnerOptions { get; set; } = [];

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (string.IsNullOrWhiteSpace(Email) && string.IsNullOrWhiteSpace(Phone))
        {
            yield return new ValidationResult(
                "Enter an email address or a phone number.",
                [nameof(Email), nameof(Phone)]);
        }
    }

    public CreateLeadCommand ToCreateCommand() =>
        new(
            Name,
            string.IsNullOrWhiteSpace(CompanyName) ? null : CompanyName.Trim(),
            string.IsNullOrWhiteSpace(Email) ? null : Email.Trim(),
            string.IsNullOrWhiteSpace(Phone) ? null : Phone.Trim(),
            Source,
            Priority,
            ExpectedValue,
            string.IsNullOrWhiteSpace(OwnerId) ? null : OwnerId,
            string.IsNullOrWhiteSpace(Notes) ? null : Notes.Trim());

    public UpdateLeadCommand ToUpdateCommand(byte[] rowVersion) =>
        new(
            Name,
            string.IsNullOrWhiteSpace(CompanyName) ? null : CompanyName.Trim(),
            string.IsNullOrWhiteSpace(Email) ? null : Email.Trim(),
            string.IsNullOrWhiteSpace(Phone) ? null : Phone.Trim(),
            Source,
            Priority,
            ExpectedValue,
            string.IsNullOrWhiteSpace(Notes) ? null : Notes.Trim(),
            rowVersion);
}

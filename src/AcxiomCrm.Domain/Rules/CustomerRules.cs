using AcxiomCrm.Domain.Validation;

namespace AcxiomCrm.Domain.Rules;

public static class CustomerRules
{
    public static IReadOnlyList<FieldError> Validate(
        string? name,
        string? company,
        string? email,
        string? phone,
        string? address,
        string? city,
        string? state,
        string? notes)
    {
        return new FieldValidator()
            .Required("Name", "Customer name", name)
            .MaxLength("Name", "Customer name", name, Limits.Name)
            .MaxLength("CompanyName", "Company name", company, Limits.Name)
            .MaxLength("Email", "Email", email, Limits.Email)
            .Email("Email", email, required: true)
            .Phone("Phone", phone, required: true)
            .MaxLength("Address", "Address", address, Limits.Address)
            .MaxLength("City", "City", city, Limits.CityState)
            .MaxLength("State", "State", state, Limits.CityState)
            .MaxLength("Notes", "Notes", notes, Limits.Notes)
            .Errors;
    }
}

using AcxiomCrm.Domain.Validation;

namespace AcxiomCrm.Domain.Rules;

public static class LeadRules
{
    public const decimal MaxExpectedValue = Limits.LeadMaxValue; // 100,000,000

    public static IReadOnlyList<FieldError> Validate(
        string? name,
        string? company,
        string? email,
        string? phone,
        decimal expectedValue,
        string? notes)
    {
        var errors = new List<FieldError>();

        if (string.IsNullOrWhiteSpace(name))
        {
            errors.Add(new FieldError("Name", "Lead name is required."));
        }
        else if (name.Length > Limits.Name)
        {
            errors.Add(new FieldError("Name", $"Lead name must not exceed {Limits.Name} characters."));
        }

        if (!string.IsNullOrEmpty(company) && company.Length > Limits.Name)
        {
            errors.Add(new FieldError("CompanyName", $"Company name must not exceed {Limits.Name} characters."));
        }

        var hasEmail = !string.IsNullOrWhiteSpace(email);
        var hasPhone = !string.IsNullOrWhiteSpace(phone);

        if (!hasEmail && !hasPhone)
        {
            errors.Add(new FieldError("Email", "Enter an email address or a phone number."));
            errors.Add(new FieldError("Phone", "Enter an email address or a phone number."));
        }
        else
        {
            if (hasEmail)
            {
                if (email!.Length > Limits.Email)
                {
                    errors.Add(new FieldError("Email", $"Email must not exceed {Limits.Email} characters."));
                }
                else if (!EmailRules.IsValid(email))
                {
                    errors.Add(new FieldError("Email", "Enter a valid email address."));
                }
            }

            if (hasPhone)
            {
                if (!PhoneRules.IsValid(phone))
                {
                    errors.Add(new FieldError("Phone", "Enter a valid phone number."));
                }
            }
        }

        if (expectedValue < 0m || expectedValue > MaxExpectedValue)
        {
            errors.Add(new FieldError("ExpectedValue", "Expected value must be between 0 and 10,00,00,000."));
        }
        else if (decimal.Round(expectedValue, 2) != expectedValue)
        {
            errors.Add(new FieldError("ExpectedValue", "Expected value cannot have more than 2 decimal places."));
        }

        if (!string.IsNullOrEmpty(notes) && notes.Length > Limits.Notes)
        {
            errors.Add(new FieldError("Notes", $"Notes must not exceed {Limits.Notes} characters."));
        }

        return errors;
    }
}

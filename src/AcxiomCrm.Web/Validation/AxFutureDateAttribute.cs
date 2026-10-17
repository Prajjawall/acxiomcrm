using System.ComponentModel.DataAnnotations;
using AcxiomCrm.Application.Abstractions;

namespace AcxiomCrm.Web.Validation;

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter, AllowMultiple = false)]
public sealed class AxFutureDateAttribute : ValidationAttribute
{
    public AxFutureDateAttribute(string label)
    {
        Label = label;
        ErrorMessage = $"{label} cannot be in the past.";
    }

    public string Label { get; }

    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        ArgumentNullException.ThrowIfNull(validationContext);

        if (value is null)
        {
            return ValidationResult.Success;
        }

        DateOnly date;
        if (value is DateOnly d)
        {
            date = d;
        }
        else if (value is DateTime dt)
        {
            date = DateOnly.FromDateTime(dt);
        }
        else if (value is string s && DateOnly.TryParse(s, out var parsed))
        {
            date = parsed;
        }
        else
        {
            return ValidationResult.Success;
        }

        var clock = validationContext.GetService(typeof(IClock)) as IClock
            ?? throw new InvalidOperationException("IClock service is required for AxFutureDate validation.");
        var today = clock.Today;

        if (date < today)
        {
            var message = string.IsNullOrEmpty(ErrorMessage)
                ? $"{Label} cannot be in the past."
                : ErrorMessage;
            return new ValidationResult(message);
        }

        return ValidationResult.Success;
    }
}

using System.ComponentModel.DataAnnotations;

namespace AcxiomCrm.Infrastructure.Options;

public sealed class BusinessOptions : IValidatableObject
{
    public const string Section = "Business";

    [Required]
    public string TimeZone { get; set; } = "Asia/Kolkata";

    [Required]
    public string Currency { get; set; } = "INR";

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        var invalid = false;
        try
        {
            _ = TimeZoneInfo.FindSystemTimeZoneById(TimeZone);
        }
        catch (Exception)
        {
            invalid = true;
        }

        if (invalid)
        {
            yield return new ValidationResult(
                $"Time zone '{TimeZone}' is not recognised by the operating system.",
                [nameof(TimeZone)]);
        }
    }
}

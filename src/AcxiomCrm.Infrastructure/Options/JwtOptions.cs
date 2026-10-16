using System.ComponentModel.DataAnnotations;
using System.Text;

namespace AcxiomCrm.Infrastructure.Options;

public sealed class JwtOptions : IValidatableObject
{
    public const string Section = "Security:Jwt";

    public bool Enabled { get; set; }

    public string Issuer { get; set; } = "AcxiomCrm";

    public string Audience { get; set; } = "AcxiomCrm";

    public string SigningKey { get; set; } = string.Empty;

    [Range(1, 1440)]
    public int AccessMinutes { get; set; } = 15;

    [Range(1, 90)]
    public int RefreshDays { get; set; } = 7;

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (Enabled)
        {
            if (string.IsNullOrWhiteSpace(SigningKey) || Encoding.UTF8.GetByteCount(SigningKey) < 32)
            {
                yield return new ValidationResult(
                    "JWT SigningKey must be at least 32 bytes when JWT authentication is enabled.",
                    [nameof(SigningKey)]);
            }
        }
    }
}

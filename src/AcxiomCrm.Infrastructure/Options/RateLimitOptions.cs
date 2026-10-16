using System.ComponentModel.DataAnnotations;

namespace AcxiomCrm.Infrastructure.Options;

public sealed class RateLimitOptions
{
    public const string Section = "Security:RateLimit";

    [Range(1, 1000)]
    public int AuthPermitPerMinute { get; set; } = 10;
}

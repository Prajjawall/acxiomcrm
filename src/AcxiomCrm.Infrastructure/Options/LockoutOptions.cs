using System.ComponentModel.DataAnnotations;

namespace AcxiomCrm.Infrastructure.Options;

public sealed class LockoutOptions
{
    public const string Section = "Security:Lockout";

    [Range(3, 20)]
    public int MaxFailedAttempts { get; set; } = 5;

    [Range(1, 1440)]
    public int LockoutMinutes { get; set; } = 15;
}

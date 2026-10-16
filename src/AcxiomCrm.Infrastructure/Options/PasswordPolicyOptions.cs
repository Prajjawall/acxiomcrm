using System.ComponentModel.DataAnnotations;

namespace AcxiomCrm.Infrastructure.Options;

public sealed class PasswordPolicyOptions
{
    public const string Section = "Security:Password";

    [Range(8, 128)]
    public int MinLength { get; set; } = 12;

    public bool RequireUpper { get; set; } = true;

    public bool RequireLower { get; set; } = true;

    public bool RequireDigit { get; set; } = true;

    public bool RequireSymbol { get; set; } = true;

    public IReadOnlyList<string> DenyList { get; set; } = [];
}

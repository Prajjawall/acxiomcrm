using System.ComponentModel.DataAnnotations;

namespace AcxiomCrm.Infrastructure.Options;

public sealed class RegistrationOptions
{
    public const string Section = "Registration";

    [Required]
    public string Mode { get; set; } = "Open";
}

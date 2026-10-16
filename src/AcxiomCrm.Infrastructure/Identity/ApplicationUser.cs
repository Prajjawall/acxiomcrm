using Microsoft.AspNetCore.Identity;

namespace AcxiomCrm.Infrastructure.Identity;

public sealed class ApplicationUser : IdentityUser
{
    public string FullName { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public string? ManagerId { get; set; }
    public bool MustChangePassword { get; set; }
    public DateTime CreatedDate { get; set; }
    public DateTime? LastLoginDate { get; set; }
}

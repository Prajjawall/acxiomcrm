namespace AcxiomCrm.Application.Users;

public sealed record UserSummary(
    string Id,
    string FullName,
    string UserName,
    string Email,
    string Role,
    bool IsActive,
    string? ManagerId,
    bool LockedOut,
    DateTimeOffset? LockoutEnd,
    bool MustChangePassword,
    DateTime CreatedDate,
    DateTime? LastLoginDate);

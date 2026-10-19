namespace AcxiomCrm.Application.Users;

public sealed record UserFilter(
    string? Search = null,
    string? Role = null,
    bool? Active = null,
    bool? LockedOut = null,
    string? ManagerId = null);

public sealed record UserListItem(
    string Id,
    string FullName,
    string UserName,
    string Email,
    string Role,
    bool IsActive,
    bool LockedOut,
    DateTime? LockoutEndUtc,
    string? ManagerId,
    string? ManagerName,
    DateTime? LastLoginDateUtc,
    DateTime CreatedDate);

public sealed record UserDetail(
    string Id,
    string FullName,
    string UserName,
    string Email,
    string Role,
    bool IsActive,
    bool LockedOut,
    DateTime? LockoutEndUtc,
    string? ManagerId,
    string? ManagerName,
    bool MustChangePassword,
    DateTime? LastLoginDateUtc,
    DateTime CreatedDate,
    int OpenLeads,
    int OpenOpportunities,
    int PlannedFollowUps);

public sealed record CreateUserCommand(
    string FullName,
    string UserName,
    string Email,
    string Role,
    string? ManagerId,
    bool IsActive);

public sealed record UpdateUserCommand(
    string FullName,
    string UserName,
    string Email,
    string Role,
    string? ManagerId);

public sealed record DeactivateUserCommand(
    string? ReassignToUserId = null);

public sealed record CreatedUserResult(
    string UserId,
    string TemporaryPassword);

public sealed record RoleSummary(
    string Name,
    string DisplayName,
    int MemberCount,
    IReadOnlyList<string> Permissions);

namespace AcxiomCrm.Application.Auth;

public sealed record LoginCommand(string UserNameOrEmail, string Password);

public enum LoginStatus
{
    Succeeded,
    InvalidCredentials,
    LockedOut,
    MustChangePassword
}

public sealed record AuthOutcome(LoginStatus Status, string? UserId = null, DateTime? LockoutEndUtc = null);

public sealed record RegisterCommand(string FullName, string UserName, string Email, string Password);

public sealed record ChangePasswordCommand(string CurrentPassword, string NewPassword);

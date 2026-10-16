using AcxiomCrm.Application.Abstractions;
using AcxiomCrm.Application.Auth;
using AcxiomCrm.Application.Common;
using AcxiomCrm.Domain.Enums;
using AcxiomCrm.Domain.Security;
using AcxiomCrm.Domain.Validation;
using AcxiomCrm.Infrastructure.Identity;
using AcxiomCrm.Infrastructure.Options;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace AcxiomCrm.Infrastructure.Auth;

public sealed class AuthService(
    UserManager<ApplicationUser> userManager,
    SignInManager<ApplicationUser> signInManager,
    IAuditWriter auditWriter,
    ICurrentUser currentUser,
    IClock clock,
    IOptions<RegistrationOptions> registrationOptions) : IAuthService
{
    public async Task<AuthOutcome> AuthenticateAsync(LoginCommand cmd, CancellationToken ct = default)
    {
        var input = cmd.UserNameOrEmail?.Trim() ?? string.Empty;
        var trimmedUser = input.Length > 100 ? input[..100] : input;

        ApplicationUser? user;
        if (input.Contains('@', StringComparison.Ordinal))
        {
            user = await userManager.FindByEmailAsync(input) ?? await userManager.FindByNameAsync(input);
        }
        else
        {
            user = await userManager.FindByNameAsync(input) ?? await userManager.FindByEmailAsync(input);
        }

        if (user == null)
        {
            await auditWriter.WriteAsync(new AuditEntry("LoginFailed", "User", null, AuditResult.Failure, Details: $"unknown-user;user={trimmedUser}"), ct);
            return new AuthOutcome(LoginStatus.InvalidCredentials);
        }

        if (!user.IsActive)
        {
            await auditWriter.WriteAsync(new AuditEntry("LoginFailed", "User", user.Id, AuditResult.Failure, Details: $"inactive;user={trimmedUser}"), ct);
            return new AuthOutcome(LoginStatus.InvalidCredentials);
        }

        var signInResult = await signInManager.CheckPasswordSignInAsync(user, cmd.Password, lockoutOnFailure: true);

        if (signInResult.IsLockedOut)
        {
            await auditWriter.WriteAsync(new AuditEntry("Lockout", "User", user.Id, AuditResult.Failure, Details: $"user={trimmedUser}"), ct);
            return new AuthOutcome(LoginStatus.LockedOut, user.Id, user.LockoutEnd?.UtcDateTime);
        }

        if (!signInResult.Succeeded)
        {
            // Check if this failure locked out the user
            if (await userManager.IsLockedOutAsync(user))
            {
                await auditWriter.WriteAsync(new AuditEntry("Lockout", "User", user.Id, AuditResult.Failure, Details: $"user={trimmedUser}"), ct);
                return new AuthOutcome(LoginStatus.LockedOut, user.Id, user.LockoutEnd?.UtcDateTime);
            }

            await auditWriter.WriteAsync(new AuditEntry("LoginFailed", "User", user.Id, AuditResult.Failure, Details: $"bad-password;user={trimmedUser}"), ct);
            return new AuthOutcome(LoginStatus.InvalidCredentials);
        }

        user.LastLoginDate = clock.UtcNow;
        await userManager.UpdateAsync(user);

        await auditWriter.WriteAsync(new AuditEntry("Login", "User", user.Id, AuditResult.Success, Details: null), ct);

        if (user.MustChangePassword)
        {
            return new AuthOutcome(LoginStatus.MustChangePassword, user.Id);
        }

        return new AuthOutcome(LoginStatus.Succeeded, user.Id);
    }

    public async Task<Result<string>> RegisterAsync(RegisterCommand cmd, CancellationToken ct = default)
    {
        var mode = registrationOptions.Value.Mode;
        if (string.Equals(mode, "Disabled", StringComparison.OrdinalIgnoreCase))
        {
            return Result<string>.Fail(ErrorKind.NotFound, new FieldError(string.Empty, "Registration is disabled."));
        }

        var errors = new List<FieldError>();

        if (string.IsNullOrWhiteSpace(cmd.FullName))
        {
            errors.Add(new FieldError(nameof(cmd.FullName), "Full name is required."));
        }
        else if (cmd.FullName.Length > Limits.Name)
        {
            errors.Add(new FieldError(nameof(cmd.FullName), "Full name cannot exceed 150 characters."));
        }

        if (string.IsNullOrWhiteSpace(cmd.Email))
        {
            errors.Add(new FieldError(nameof(cmd.Email), "Email is required."));
        }
        else if (!EmailRules.IsValid(cmd.Email))
        {
            errors.Add(new FieldError(nameof(cmd.Email), "Invalid email format."));
        }

        if (string.IsNullOrWhiteSpace(cmd.UserName))
        {
            errors.Add(new FieldError(nameof(cmd.UserName), "Username is required."));
        }

        if (string.IsNullOrWhiteSpace(cmd.Password))
        {
            errors.Add(new FieldError(nameof(cmd.Password), "Password is required."));
        }

        if (errors.Count > 0)
        {
            return Result<string>.Fail(ErrorKind.Validation, errors);
        }

        var normalizedEmail = cmd.Email.Trim();
        var existingEmail = await userManager.FindByEmailAsync(normalizedEmail);
        if (existingEmail != null)
        {
            errors.Add(new FieldError(nameof(cmd.Email), "Email is already registered."));
        }

        var normalizedUserName = cmd.UserName.Trim();
        var existingUser = await userManager.FindByNameAsync(normalizedUserName);
        if (existingUser != null)
        {
            errors.Add(new FieldError(nameof(cmd.UserName), "Username is already taken."));
        }

        if (errors.Count > 0)
        {
            return Result<string>.Fail(ErrorKind.Validation, errors);
        }

        var isActive = !string.Equals(mode, "ApprovalRequired", StringComparison.OrdinalIgnoreCase);

        var user = new ApplicationUser
        {
            FullName = cmd.FullName.Trim(),
            UserName = normalizedUserName,
            Email = normalizedEmail,
            IsActive = isActive,
            CreatedDate = clock.UtcNow
        };

        var createResult = await userManager.CreateAsync(user, cmd.Password);
        if (!createResult.Succeeded)
        {
            foreach (var err in createResult.Errors)
            {
                if (err.Code.Contains("Password", StringComparison.OrdinalIgnoreCase))
                {
                    errors.Add(new FieldError(nameof(cmd.Password), err.Description));
                }
                else if (err.Code.Contains("Email", StringComparison.OrdinalIgnoreCase))
                {
                    errors.Add(new FieldError(nameof(cmd.Email), "Email is already registered."));
                }
                else if (err.Code.Contains("UserName", StringComparison.OrdinalIgnoreCase))
                {
                    errors.Add(new FieldError(nameof(cmd.UserName), "Username is already taken."));
                }
                else
                {
                    errors.Add(new FieldError(string.Empty, err.Description));
                }
            }

            return Result<string>.Fail(ErrorKind.Validation, errors);
        }

        await userManager.AddToRoleAsync(user, Roles.SalesExecutive);

        await auditWriter.WriteAsync(new AuditEntry("Register", "User", user.Id, AuditResult.Success, Details: null), ct);

        return Result<string>.Ok(user.Id);
    }

    public async Task<Result> ChangePasswordAsync(ChangePasswordCommand cmd, CancellationToken ct = default)
    {
        if (!currentUser.IsAuthenticated || string.IsNullOrWhiteSpace(currentUser.UserId))
        {
            return Result.Fail(ErrorKind.Forbidden);
        }

        var user = await userManager.FindByIdAsync(currentUser.UserId);
        if (user == null)
        {
            return Result.Fail(ErrorKind.NotFound);
        }

        if (string.Equals(cmd.NewPassword, cmd.CurrentPassword, StringComparison.Ordinal))
        {
            return Result.Fail(ErrorKind.Validation, new FieldError(nameof(cmd.NewPassword), "New password cannot be the same as current password."));
        }

        var result = await userManager.ChangePasswordAsync(user, cmd.CurrentPassword, cmd.NewPassword);
        if (!result.Succeeded)
        {
            var errors = new List<FieldError>();
            foreach (var err in result.Errors)
            {
                if (err.Code.Equals("PasswordMismatch", StringComparison.OrdinalIgnoreCase))
                {
                    errors.Add(new FieldError(nameof(cmd.CurrentPassword), "Incorrect current password."));
                }
                else
                {
                    errors.Add(new FieldError(nameof(cmd.NewPassword), err.Description));
                }
            }

            return Result.Fail(ErrorKind.Validation, errors);
        }

        user.MustChangePassword = false;
        await userManager.UpdateAsync(user);

        await auditWriter.WriteAsync(new AuditEntry("PasswordChanged", "User", user.Id, AuditResult.Success, Details: null), ct);

        return Result.Ok();
    }

    public async Task RecordLogoutAsync(CancellationToken ct = default)
    {
        if (currentUser.IsAuthenticated && !string.IsNullOrWhiteSpace(currentUser.UserId))
        {
            await auditWriter.WriteAsync(new AuditEntry("Logout", "User", currentUser.UserId, AuditResult.Success, Details: null), ct);
        }
    }
}

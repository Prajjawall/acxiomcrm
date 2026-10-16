using Microsoft.AspNetCore.Identity;

namespace AcxiomCrm.Infrastructure.Identity;

public sealed class PasswordPolicyValidator : IPasswordValidator<ApplicationUser>
{
    public Task<IdentityResult> ValidateAsync(UserManager<ApplicationUser> manager, ApplicationUser user, string? password)
    {
        if (string.IsNullOrWhiteSpace(password))
        {
            return Task.FromResult(IdentityResult.Failed(new IdentityError
            {
                Code = "PasswordRequiresNonAlphanumeric",
                Description = "Password cannot be empty."
            }));
        }

        var errors = new List<IdentityError>();

        // 1. Password must not contain username
        if (!string.IsNullOrWhiteSpace(user.UserName) &&
            password.Contains(user.UserName, StringComparison.OrdinalIgnoreCase))
        {
            errors.Add(new IdentityError
            {
                Code = "PasswordContainsUserName",
                Description = "Password cannot contain username."
            });
        }

        // 2. Password must not contain local part of email
        if (!string.IsNullOrWhiteSpace(user.Email))
        {
            var emailParts = user.Email.Split('@');
            if (emailParts.Length > 0 && !string.IsNullOrWhiteSpace(emailParts[0]) &&
                password.Contains(emailParts[0], StringComparison.OrdinalIgnoreCase))
            {
                errors.Add(new IdentityError
                {
                    Code = "PasswordContainsEmailLocalPart",
                    Description = "Password cannot contain email username."
                });
            }
        }

        // 3. Password must not be in deny list
        if (PasswordDenyList.Contains(password))
        {
            errors.Add(new IdentityError
            {
                Code = "PasswordIsInDenyList",
                Description = "This password is too common and cannot be used."
            });
        }

        return Task.FromResult(errors.Count > 0
            ? IdentityResult.Failed([.. errors])
            : IdentityResult.Success);
    }
}

using System.Reflection;
using System.Text.RegularExpressions;
using AcxiomCrm.Application.Users;
using AcxiomCrm.Infrastructure.Identity;
using AcxiomCrm.Infrastructure.Users;
using Shouldly;

namespace AcxiomCrm.Application.Tests;

public class UserResponseShapeTests
{
    [Fact]
    public void User_models_do_not_expose_security_sensitive_fields()
    {
        var sensitivePattern = new Regex(@"Hash|Stamp|Token|Password", RegexOptions.IgnoreCase);
        var types = typeof(UserListItem).Assembly.GetTypes()
            .Where(t => t.Namespace != null && t.Namespace.StartsWith("AcxiomCrm.Application.Users", StringComparison.Ordinal))
            .ToList();

        types.ShouldNotBeEmpty();

        foreach (var type in types)
        {
            var props = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);
            foreach (var prop in props)
            {
                if (sensitivePattern.IsMatch(prop.Name))
                {
                    // Only TemporaryPassword on CreatedUserResult and MustChangePassword boolean flags are allowed
                    if (type == typeof(CreatedUserResult) && prop.Name == nameof(CreatedUserResult.TemporaryPassword))
                    {
                        continue;
                    }

                    if (prop.Name == "MustChangePassword")
                    {
                        continue;
                    }

                    throw new InvalidOperationException($"Sensitive property '{prop.Name}' found on type '{type.FullName}'.");
                }
            }
        }
    }

    [Fact]
    public void Temporary_passwords_comply_with_policy()
    {
        for (var i = 0; i < 200; i++)
        {
            var userName = $"user{i}";
            var email = $"user{i}@example.com";
            var pwd = TemporaryPasswordGenerator.Generate(userName, email, 18);

            pwd.Length.ShouldBeGreaterThanOrEqualTo(16);
            pwd.Any(char.IsUpper).ShouldBeTrue();
            pwd.Any(char.IsLower).ShouldBeTrue();
            pwd.Any(char.IsDigit).ShouldBeTrue();
            pwd.Any(ch => "!@#$%^&*()-_=+[]{}".Contains(ch)).ShouldBeTrue();

            PasswordDenyList.Contains(pwd).ShouldBeFalse();
            pwd.Contains(userName, StringComparison.OrdinalIgnoreCase).ShouldBeFalse();
            pwd.Contains($"user{i}", StringComparison.OrdinalIgnoreCase).ShouldBeFalse();
        }
    }
}

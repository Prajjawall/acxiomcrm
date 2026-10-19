using System.Security.Cryptography;
using AcxiomCrm.Infrastructure.Identity;

namespace AcxiomCrm.Infrastructure.Users;

public static class TemporaryPasswordGenerator
{
    private const string UpperChars = "ABCDEFGHJKLMNPQRSTUVWXYZ";
    private const string LowerChars = "abcdefghijkmnopqrstuvwxyz";
    private const string DigitChars = "23456789";
    private const string SpecialChars = "!@#$%^&*()-_=+[]{}";
    private static readonly string AllChars = UpperChars + LowerChars + DigitChars + SpecialChars;

    public static string Generate(string? userName = null, string? email = null, int length = 18)
    {
        if (length < 16)
        {
            length = 16;
        }

        while (true)
        {
            var chars = new char[length];
            chars[0] = UpperChars[RandomNumberGenerator.GetInt32(UpperChars.Length)];
            chars[1] = LowerChars[RandomNumberGenerator.GetInt32(LowerChars.Length)];
            chars[2] = DigitChars[RandomNumberGenerator.GetInt32(DigitChars.Length)];
            chars[3] = SpecialChars[RandomNumberGenerator.GetInt32(SpecialChars.Length)];

            for (var i = 4; i < length; i++)
            {
                chars[i] = AllChars[RandomNumberGenerator.GetInt32(AllChars.Length)];
            }

            // Fisher-Yates shuffle
            for (var i = length - 1; i > 0; i--)
            {
                var j = RandomNumberGenerator.GetInt32(i + 1);
                (chars[i], chars[j]) = (chars[j], chars[i]);
            }

            var password = new string(chars);

            if (PasswordDenyList.Contains(password))
            {
                continue;
            }

            if (!string.IsNullOrWhiteSpace(userName) && password.Contains(userName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!string.IsNullOrWhiteSpace(email))
            {
                var emailParts = email.Split('@');
                if (emailParts.Length > 0 && !string.IsNullOrWhiteSpace(emailParts[0]) &&
                    password.Contains(emailParts[0], StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
            }

            return password;
        }
    }
}

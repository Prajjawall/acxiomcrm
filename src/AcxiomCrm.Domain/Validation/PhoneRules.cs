using System.Text.RegularExpressions;

namespace AcxiomCrm.Domain.Validation;

public static class PhoneRules
{
    public const string Pattern = @"^[6-9][0-9]{9}$";
    private static readonly Regex Regex = new(Pattern, RegexOptions.Compiled);

    public static bool TryNormalize(string? raw, out string normalized)
    {
        normalized = string.Empty;

        if (string.IsNullOrWhiteSpace(raw))
        {
            return false;
        }

        // Strip whitespace, hyphens, parentheses
        var cleaned = raw
            .Replace(" ", "", StringComparison.Ordinal)
            .Replace("-", "", StringComparison.Ordinal)
            .Replace("(", "", StringComparison.Ordinal)
            .Replace(")", "", StringComparison.Ordinal);

        // Strip prefixes: +91, 91 (when remaining is 12 chars), 0 (when remaining is 11 chars)
        if (cleaned.StartsWith("+91", StringComparison.Ordinal))
        {
            cleaned = cleaned[3..];
        }
        else if (cleaned.StartsWith("91", StringComparison.Ordinal) && cleaned.Length == 12)
        {
            cleaned = cleaned[2..];
        }
        else if (cleaned.StartsWith('0') && cleaned.Length == 11)
        {
            cleaned = cleaned[1..];
        }

        if (cleaned.Length == 10 && Regex.IsMatch(cleaned))
        {
            normalized = cleaned;
            return true;
        }

        return false;
    }
}

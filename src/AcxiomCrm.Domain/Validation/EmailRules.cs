using System.Text.RegularExpressions;

namespace AcxiomCrm.Domain.Validation;

public static class EmailRules
{
    private static readonly Regex LocalPartRegex = new(
        @"^[A-Za-z0-9!#$%&'*+\-/=?^_`{|}~]+(\.[A-Za-z0-9!#$%&'*+\-/=?^_`{|}~]+)*$",
        RegexOptions.Compiled);

    private static readonly Regex DomainLabelRegex = new(
        @"^[A-Za-z0-9]([A-Za-z0-9-]*[A-Za-z0-9])?$",
        RegexOptions.Compiled);

    private static readonly Regex TldRegex = new(
        @"^[A-Za-z]{2,}$",
        RegexOptions.Compiled);

    public static bool IsValid(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return false;
        }

        var trimmed = raw.Trim();
        if (trimmed.Length > Limits.Email)
        {
            return false;
        }

        var atIndex = trimmed.IndexOf('@', StringComparison.Ordinal);
        if (atIndex <= 0 || atIndex != trimmed.LastIndexOf('@'))
        {
            return false; // missing '@' or multiple '@' or leading '@'
        }

        var localPart = trimmed[..atIndex];
        var domainPart = trimmed[(atIndex + 1)..];

        if (localPart.Length == 0 || localPart.Length > 64 || !LocalPartRegex.IsMatch(localPart))
        {
            return false;
        }

        if (domainPart.Length == 0 || domainPart.Length > 253)
        {
            return false;
        }

        var labels = domainPart.Split('.');
        if (labels.Length < 2)
        {
            return false; // must have at least domain and TLD
        }

        for (var i = 0; i < labels.Length - 1; i++)
        {
            var label = labels[i];
            if (label.Length == 0 || label.Length > 63 || !DomainLabelRegex.IsMatch(label))
            {
                return false;
            }
        }

        var tld = labels[^1];
        if (!TldRegex.IsMatch(tld))
        {
            return false;
        }

        return true;
    }

    public static string Normalize(string raw)
    {
        return raw.Trim().ToLowerInvariant();
    }
}

using System.Text.RegularExpressions;
using AcxiomCrm.Domain.Security;

namespace AcxiomCrm.Web.Presentation;

public static partial class Labels
{
    private static readonly Dictionary<string, string> FixedLabels = new(StringComparer.OrdinalIgnoreCase)
    {
        [Roles.Admin] = "Administrator",
        [Roles.Manager] = "Manager",
        [Roles.SalesExecutive] = "Sales executive",
        ["Won"] = "Won",
        ["Lost"] = "Lost",
        ["Open"] = "Open",
        ["ApprovalRequired"] = "Approval required",
        ["Disabled"] = "Disabled"
    };

    public static string For(object? value)
    {
        if (value == null)
        {
            return string.Empty;
        }

        var text = value.ToString() ?? string.Empty;
        if (FixedLabels.TryGetValue(text, out var fixedLabel))
        {
            return fixedLabel;
        }

        // Convert PascalCase to sentence case (e.g. SalesExecutive -> Sales executive)
        var spaced = PascalCaseRegex().Replace(text, " $1").Trim();
        if (spaced.Length <= 1)
        {
            return spaced;
        }

        return char.ToUpperInvariant(spaced[0]) + spaced[1..].ToLowerInvariant();
    }

    [GeneratedRegex("(?<!^)(?=[A-Z])")]
    private static partial Regex PascalCaseRegex();
}

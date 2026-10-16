namespace AcxiomCrm.Application.Common;

public static class SearchHelper
{
    public static string? Normalise(string? term)
    {
        if (string.IsNullOrWhiteSpace(term))
        {
            return null;
        }

        var trimmed = term.Trim();
        return trimmed.Length > 100 ? trimmed[..100] : trimmed;
    }

    public static string EscapeLike(string term)
    {
        if (string.IsNullOrEmpty(term))
        {
            return string.Empty;
        }

        // Must escape backslash first, then %, _, [
        return term
            .Replace(@"\", @"\\", StringComparison.Ordinal)
            .Replace("%", @"\%", StringComparison.Ordinal)
            .Replace("_", @"\_", StringComparison.Ordinal)
            .Replace("[", @"\[", StringComparison.Ordinal);
    }
}

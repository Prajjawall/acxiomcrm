namespace AcxiomCrm.Infrastructure.Audit;

public static class SensitiveProperties
{
    private static readonly HashSet<string> ExactSensitive = new(StringComparer.OrdinalIgnoreCase)
    {
        "Password",
        "PasswordHash",
        "SecurityStamp",
        "ConcurrencyStamp",
        "Token",
        "Secret",
        "ApiKey",
        "CreditCard"
    };

    public static bool IsSensitive(string propertyName)
    {
        if (ExactSensitive.Contains(propertyName))
        {
            return true;
        }

        if (propertyName.Contains("Password", StringComparison.OrdinalIgnoreCase) ||
            propertyName.Contains("Token", StringComparison.OrdinalIgnoreCase) ||
            propertyName.Contains("Secret", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return false;
    }
}

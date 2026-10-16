namespace AcxiomCrm.Infrastructure.Identity;

public static class PasswordDenyList
{
    private static readonly HashSet<string> DeniedPasswords = new(StringComparer.OrdinalIgnoreCase)
    {
        "123456", "password", "12345678", "qwerty", "123456789", "12345", "1234", "111111",
        "1234567", "dragon", "123123", "baseball", "football", "monkey", "letmein", "shadow",
        "master", "666666", "mustang", "123321", "superman", "harley", "654321", "michael",
        "charlie", "trustno1", "admin", "welcome", "Password123!", "Password123", "Password1234",
        "Password!123", "Welcome123!", "Welcome123", "Admin123!", "Admin123", "Pass1234!",
        "Pass1234", "ChangeMe123!", "ChangeMe123", "Secret123!", "Secret123", "Summer2026!",
        "Winter2026!", "Spring2026!", "Autumn2026!", "Acxiom123!", "Acxiom123", "CrmPassword1!",
        "Testing123!", "Testing123", "Qwerty123!"
    };

    public static bool Contains(string password)
    {
        if (string.IsNullOrWhiteSpace(password))
        {
            return false;
        }

        return DeniedPasswords.Contains(password);
    }
}

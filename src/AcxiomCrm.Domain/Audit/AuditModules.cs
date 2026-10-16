namespace AcxiomCrm.Domain.Audit;

public static class AuditModules
{
    public const string Authentication = "Authentication";
    public const string Customers = "Customers";
    public const string Leads = "Leads";
    public const string Opportunities = "Opportunities";
    public const string FollowUps = "FollowUps";
    public const string Activities = "Activities";
    public const string Users = "Users";
    public const string Security = "Security";

    public static string ForEntity(string? entityName)
    {
        if (string.IsNullOrWhiteSpace(entityName)) return "System";
        if (entityName.StartsWith("Customer", StringComparison.OrdinalIgnoreCase)) return Customers;
        if (entityName.StartsWith("Lead", StringComparison.OrdinalIgnoreCase)) return Leads;
        if (entityName.StartsWith("Opportunity", StringComparison.OrdinalIgnoreCase)) return Opportunities;
        if (entityName.StartsWith("FollowUp", StringComparison.OrdinalIgnoreCase)) return FollowUps;
        if (entityName.StartsWith("Activity", StringComparison.OrdinalIgnoreCase)) return Activities;
        if (entityName.StartsWith("User", StringComparison.OrdinalIgnoreCase)) return Users;
        if (entityName.StartsWith("Auth", StringComparison.OrdinalIgnoreCase)) return Authentication;
        return entityName;
    }
}

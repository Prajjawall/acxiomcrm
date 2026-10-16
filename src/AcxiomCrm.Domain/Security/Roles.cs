namespace AcxiomCrm.Domain.Security;

public static class Roles
{
    public const string Admin = "Admin";
    public const string Manager = "Manager";
    public const string SalesExecutive = "SalesExecutive";

    public static readonly IReadOnlyList<string> All = [Admin, Manager, SalesExecutive];
}

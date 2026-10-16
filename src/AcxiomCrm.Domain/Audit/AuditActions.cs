namespace AcxiomCrm.Domain.Audit;

public static class AuditActions
{
    public const string Login = "Login";
    public const string LoginFailed = "LoginFailed";
    public const string Lockout = "Lockout";
    public const string Unlock = "Unlock";
    public const string Logout = "Logout";
    public const string Register = "Register";
    public const string PasswordChanged = "PasswordChanged";
    public const string PasswordReset = "PasswordReset";
    public const string Create = "Create";
    public const string Update = "Update";
    public const string Delete = "Delete";
    public const string RoleChanged = "RoleChanged";
    public const string StatusChanged = "StatusChanged";
    public const string StageChanged = "StageChanged";
    public const string Assigned = "Assigned";
    public const string Converted = "Converted";
    public const string FollowUpCompleted = "FollowUpCompleted";
    public const string FollowUpRescheduled = "FollowUpRescheduled";
    public const string FollowUpMissed = "FollowUpMissed";
    public const string Activated = "Activated";
    public const string Deactivated = "Deactivated";
}

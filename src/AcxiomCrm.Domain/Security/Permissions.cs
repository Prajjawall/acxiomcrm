namespace AcxiomCrm.Domain.Security;

public static class Permissions
{
    public const string DashboardView = "dashboard.view";

    public const string CustomersView = "customers.view";
    public const string CustomersCreate = "customers.create";
    public const string CustomersEdit = "customers.edit";
    public const string CustomersDelete = "customers.delete";

    public const string LeadsView = "leads.view";
    public const string LeadsCreate = "leads.create";
    public const string LeadsEdit = "leads.edit";
    public const string LeadsDelete = "leads.delete";
    public const string LeadsAssign = "leads.assign";
    public const string LeadsConvert = "leads.convert";

    public const string OpportunitiesView = "opportunities.view";
    public const string OpportunitiesCreate = "opportunities.create";
    public const string OpportunitiesEdit = "opportunities.edit";
    public const string OpportunitiesDelete = "opportunities.delete";
    public const string OpportunitiesReopen = "opportunities.reopen";

    public const string FollowUpsView = "followups.view";
    public const string FollowUpsCreate = "followups.create";
    public const string FollowUpsEdit = "followups.edit";
    public const string FollowUpsDelete = "followups.delete";
    public const string FollowUpsHistorical = "followups.historical";

    public const string ActivitiesView = "activities.view";
    public const string ActivitiesCreate = "activities.create";
    public const string ActivitiesEdit = "activities.edit";
    public const string ActivitiesDelete = "activities.delete";

    public const string UsersView = "users.view";
    public const string UsersManage = "users.manage";
    public const string UsersUnlock = "users.unlock";
    public const string UsersResetPassword = "users.resetpassword";

    public const string RolesView = "roles.view";

    public const string AuditView = "audit.view";
    public const string AuditViewTeam = "audit.view.team";

    public const string ReportsCustomers = "reports.customers";
    public const string ReportsLeads = "reports.leads";
    public const string ReportsFollowUps = "reports.followups";
    public const string ReportsOpportunities = "reports.opportunities";
    public const string ReportsPipeline = "reports.pipeline";
    public const string ReportsConversion = "reports.conversion";
    public const string ReportsUserActivity = "reports.useractivity";
    public const string ReportsAudit = "reports.audit";

    public const string ApiAccess = "api.access";

    public static readonly IReadOnlyList<string> All =
    [
        DashboardView,
        CustomersView, CustomersCreate, CustomersEdit, CustomersDelete,
        LeadsView, LeadsCreate, LeadsEdit, LeadsDelete, LeadsAssign, LeadsConvert,
        OpportunitiesView, OpportunitiesCreate, OpportunitiesEdit, OpportunitiesDelete, OpportunitiesReopen,
        FollowUpsView, FollowUpsCreate, FollowUpsEdit, FollowUpsDelete, FollowUpsHistorical,
        ActivitiesView, ActivitiesCreate, ActivitiesEdit, ActivitiesDelete,
        UsersView, UsersManage, UsersUnlock, UsersResetPassword,
        RolesView,
        AuditView, AuditViewTeam,
        ReportsCustomers, ReportsLeads, ReportsFollowUps, ReportsOpportunities,
        ReportsPipeline, ReportsConversion, ReportsUserActivity, ReportsAudit,
        ApiAccess
    ];
}

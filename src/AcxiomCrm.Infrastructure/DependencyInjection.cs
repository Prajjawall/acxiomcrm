using AcxiomCrm.Application.Abstractions;
using AcxiomCrm.Infrastructure.Email;
using AcxiomCrm.Infrastructure.Identity;
using AcxiomCrm.Infrastructure.Options;
using AcxiomCrm.Infrastructure.Persistence;
using AcxiomCrm.Infrastructure.Time;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AcxiomCrm.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<BusinessOptions>()
            .Bind(configuration.GetSection(BusinessOptions.Section))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddOptions<PasswordPolicyOptions>()
            .Bind(configuration.GetSection(PasswordPolicyOptions.Section))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddOptions<LockoutOptions>()
            .Bind(configuration.GetSection(LockoutOptions.Section))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddOptions<JwtOptions>()
            .Bind(configuration.GetSection(JwtOptions.Section))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddOptions<RateLimitOptions>()
            .Bind(configuration.GetSection(RateLimitOptions.Section))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddOptions<RegistrationOptions>()
            .Bind(configuration.GetSection(RegistrationOptions.Section))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddOptions<AuditOptions>()
            .Bind(configuration.GetSection(AuditOptions.Section))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddOptions<DatabaseOptions>()
            .Bind(configuration.GetSection(DatabaseOptions.Section))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddSingleton<IClock, SystemClock>();
        services.AddTransient<IEmailSender, LoggingEmailSender>();

        services.AddScoped<IAuditActionHint, AcxiomCrm.Application.Common.AuditActionHint>();
        services.AddScoped(sp => new Audit.AuditSaveChangesInterceptor(
            sp.GetRequiredService<IClock>(),
            sp.GetService<ICurrentUser>(),
            sp.GetService<IAuditActionHint>()));

        var connectionString = configuration.GetConnectionString("Default");
        if (!string.IsNullOrEmpty(connectionString))
        {
            services.AddDbContext<CrmDbContext>((sp, options) =>
            {
                options.UseSqlServer(connectionString, sql => sql.EnableRetryOnFailure());
                options.AddInterceptors(sp.GetRequiredService<Audit.AuditSaveChangesInterceptor>());
            });
            services.AddDbContextFactory<CrmDbContext>(options =>
                options.UseSqlServer(connectionString, sql => sql.EnableRetryOnFailure()));
            services.AddScoped<ICrmDbContext>(sp => sp.GetRequiredService<CrmDbContext>());
        }

        services.AddScoped<IAuditWriter, Audit.AuditWriter>();
        services.AddScoped<AcxiomCrm.Application.Audit.IAuditQueryService, Audit.AuditQueryService>();
        services.AddScoped<IOwnerScope, Scope.OwnerScopeResolver>();
        services.AddScoped<AcxiomCrm.Application.Auth.IAuthService, Auth.AuthService>();
        services.AddScoped<AcxiomCrm.Application.Auth.ITokenService, Auth.TokenService>();
        services.AddScoped<AcxiomCrm.Application.Users.IUserDirectory, Users.UserDirectory>();
        services.AddScoped<AcxiomCrm.Application.Customers.ICustomerService, Customers.CustomerService>();
        services.AddScoped<AcxiomCrm.Application.Opportunities.IOpportunityService, Opportunities.OpportunityService>();
        services.AddScoped<AcxiomCrm.Application.Leads.IOpportunityFactory, Opportunities.OpportunityFactory>();
        services.AddScoped<AcxiomCrm.Application.Leads.ILeadService, Leads.LeadService>();
        services.AddScoped<AcxiomCrm.Application.FollowUps.IFollowUpService, FollowUps.FollowUpService>();
        services.AddScoped<AcxiomCrm.Application.Activities.IActivityService, Activities.ActivityService>();
        services.AddScoped<Users.IWorkReassigner, Users.WorkReassigner>();
        services.AddScoped<IRefreshTokenRevoker, Users.NoOpRefreshTokenRevoker>();
        services.AddScoped<AcxiomCrm.Application.Users.IUserAdminService, Users.UserAdminService>();
        services.AddScoped<AcxiomCrm.Application.Dashboard.IDashboardService, Dashboard.DashboardService>();
        services.AddScoped<AcxiomCrm.Application.Reports.IReportService, Reports.ReportService>();
        services.AddScoped<Seeding.DemoDataSeeder>();

        services.AddCrmIdentity(configuration);

        return services;
    }
}

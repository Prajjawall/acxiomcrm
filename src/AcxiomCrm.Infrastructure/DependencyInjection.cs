using AcxiomCrm.Application.Abstractions;
using AcxiomCrm.Infrastructure.Email;
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

        var connectionString = configuration.GetConnectionString("Default");
        if (!string.IsNullOrEmpty(connectionString))
        {
            services.AddDbContext<CrmDbContext>(options =>
                options.UseSqlServer(connectionString, sql => sql.EnableRetryOnFailure()));
            services.AddScoped<ICrmDbContext>(sp => sp.GetRequiredService<CrmDbContext>());
        }

        return services;
    }
}

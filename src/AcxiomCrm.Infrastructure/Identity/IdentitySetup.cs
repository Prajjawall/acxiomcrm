using AcxiomCrm.Infrastructure.Options;
using AcxiomCrm.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AcxiomCrm.Infrastructure.Identity;

public static class IdentitySetup
{
    public static IServiceCollection AddCrmIdentity(this IServiceCollection services, IConfiguration configuration)
    {
        var passwordSection = configuration.GetSection(PasswordPolicyOptions.Section);
        var minLength = passwordSection.GetValue<int?>("MinLength") ?? 12;
        var requireUpper = passwordSection.GetValue<bool?>("RequireUpper") ?? true;
        var requireLower = passwordSection.GetValue<bool?>("RequireLower") ?? true;
        var requireDigit = passwordSection.GetValue<bool?>("RequireDigit") ?? true;
        var requireSymbol = passwordSection.GetValue<bool?>("RequireSymbol") ?? true;

        var lockoutSection = configuration.GetSection(Options.LockoutOptions.Section);
        var maxFailed = lockoutSection.GetValue<int?>("MaxFailedAccessAttempts") ?? 5;
        var lockoutMinutes = lockoutSection.GetValue<int?>("DefaultLockoutMinutes") ?? 15;

        services.AddDataProtection();

        services.AddIdentityCore<ApplicationUser>(options =>
        {
            options.User.RequireUniqueEmail = true;
            options.SignIn.RequireConfirmedAccount = false;

            options.Password.RequiredLength = minLength;
            options.Password.RequireUppercase = requireUpper;
            options.Password.RequireLowercase = requireLower;
            options.Password.RequireDigit = requireDigit;
            options.Password.RequireNonAlphanumeric = requireSymbol;
            options.Password.RequiredUniqueChars = 1;

            options.Lockout.AllowedForNewUsers = true;
            options.Lockout.MaxFailedAccessAttempts = maxFailed;
            options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(lockoutMinutes);
        })
        .AddRoles<ApplicationRole>()
        .AddEntityFrameworkStores<CrmDbContext>()
        .AddSignInManager()
        .AddDefaultTokenProviders()
        .AddPasswordValidator<PasswordPolicyValidator>();

        services.Configure<PasswordHasherOptions>(options =>
        {
            options.IterationCount = 600_000;
        });

        services.AddScoped<RoleSeeder>();
        services.AddScoped<BootstrapAdminSeeder>();

        return services;
    }
}

using AcxiomCrm.Application.Abstractions;
using AcxiomCrm.Domain.Security;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AcxiomCrm.Infrastructure.Identity;

public sealed class BootstrapAdminSeeder(
    UserManager<ApplicationUser> userManager,
    IConfiguration configuration,
    IHostEnvironment environment,
    IClock clock,
    ILogger<BootstrapAdminSeeder> logger)
{
    public async Task SeedAsync(CancellationToken ct = default)
    {
        var existingAdmins = await userManager.GetUsersInRoleAsync(Roles.Admin);
        if (existingAdmins.Count > 0)
        {
            return;
        }

        var email = configuration["Bootstrap:AdminEmail"]?.Trim();
        var password = configuration["Bootstrap:AdminPassword"];
        var fullName = configuration["Bootstrap:AdminFullName"]?.Trim() ?? "System Administrator";

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            if (environment.IsProduction())
            {
                throw new InvalidOperationException("Bootstrap admin configuration (Bootstrap:AdminEmail, Bootstrap:AdminPassword) is missing in Production.");
            }

            logger.LogWarning("Bootstrap admin configuration is missing in non-Production environment. Skipping bootstrap admin creation.");
            return;
        }

        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            FullName = fullName,
            IsActive = true,
            EmailConfirmed = true,
            CreatedDate = clock.UtcNow
        };

        // Validate password against validators
        foreach (var validator in userManager.PasswordValidators)
        {
            var valResult = await validator.ValidateAsync(userManager, user, password);
            if (!valResult.Succeeded)
            {
                var errorSummary = string.Join("; ", valResult.Errors.Select(e => e.Code));
                throw new InvalidOperationException($"Bootstrap admin password failed policy validation: {errorSummary}.");
            }
        }

        var createResult = await userManager.CreateAsync(user, password);
        if (!createResult.Succeeded)
        {
            var errors = string.Join("; ", createResult.Errors.Select(e => e.Description));
            throw new InvalidOperationException($"Failed to create bootstrap admin user: {errors}");
        }

        var roleResult = await userManager.AddToRoleAsync(user, Roles.Admin);
        if (!roleResult.Succeeded)
        {
            var errors = string.Join("; ", roleResult.Errors.Select(e => e.Description));
            throw new InvalidOperationException($"Failed to add bootstrap admin user to Admin role: {errors}");
        }

        if (logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation("Successfully created bootstrap admin user.");
        }
    }
}

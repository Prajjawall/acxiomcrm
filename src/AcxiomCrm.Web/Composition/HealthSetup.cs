using AcxiomCrm.Infrastructure.Persistence;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;

namespace AcxiomCrm.Web.Composition;

public static class HealthSetup
{
    private static readonly string[] ReadyTags = ["ready"];

    public static IServiceCollection AddAppHealthChecks(this IServiceCollection services)
    {
        services.AddHealthChecks()
            .AddDbContextCheck<CrmDbContext>(
                name: "database",
                tags: ReadyTags);

        return services;
    }

    public static IEndpointRouteBuilder MapAppHealthChecks(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapHealthChecks("/health/live", new HealthCheckOptions
        {
            Predicate = _ => false
        });

        endpoints.MapHealthChecks("/health/ready", new HealthCheckOptions
        {
            Predicate = check => check.Tags.Contains("ready")
        });

        return endpoints;
    }
}

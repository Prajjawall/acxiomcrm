using AcxiomCrm.Web.Authorization;
using Microsoft.AspNetCore.Authorization;

namespace AcxiomCrm.Web.Composition;

public static class AuthorizationSetup
{
    public static IServiceCollection AddAppAuthorization(this IServiceCollection services)
    {
        services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();
        services.AddScoped<IAuthorizationHandler, PermissionHandler>();

        return services;
    }
}

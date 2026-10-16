using AcxiomCrm.Domain.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace AcxiomCrm.Web.Authorization;

public sealed class PermissionPolicyProvider(IOptions<AuthorizationOptions> options) : DefaultAuthorizationPolicyProvider(options)
{
    public override async Task<AuthorizationPolicy?> GetPolicyAsync(string policyName)
    {
        var existingPolicy = await base.GetPolicyAsync(policyName);
        if (existingPolicy != null)
        {
            return existingPolicy;
        }

        if (Permissions.All.Contains(policyName, StringComparer.Ordinal))
        {
            var policy = new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .AddRequirements(new PermissionRequirement(policyName))
                .Build();

            return policy;
        }

        // Unknown policy -> fallback deny all policy
        return new AuthorizationPolicyBuilder()
            .RequireAssertion(_ => false)
            .Build();
    }
}

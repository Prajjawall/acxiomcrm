using System.Security.Claims;
using AcxiomCrm.Domain.Security;
using AcxiomCrm.Infrastructure.Options;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace AcxiomCrm.Web.Authorization;

public sealed class PermissionHandler(IOptions<AuditOptions> auditOptions) : AuthorizationHandler<PermissionRequirement>
{
    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, PermissionRequirement requirement)
    {
        var role = context.User.FindFirstValue(ClaimTypes.Role);
        if (string.IsNullOrEmpty(role))
        {
            return Task.CompletedTask;
        }

        if (RolePermissionMap.Has(role, requirement.Permission))
        {
            if (string.Equals(requirement.Permission, Permissions.AuditViewTeam, StringComparison.OrdinalIgnoreCase)
                && string.Equals(role, Roles.Manager, StringComparison.OrdinalIgnoreCase)
                && !auditOptions.Value.ManagerAccess)
            {
                return Task.CompletedTask;
            }

            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}

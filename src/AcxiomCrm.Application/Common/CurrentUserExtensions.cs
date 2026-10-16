using AcxiomCrm.Application.Abstractions;
using AcxiomCrm.Domain.Security;

namespace AcxiomCrm.Application.Common;

public static class CurrentUserExtensions
{
    public static bool Can(this ICurrentUser user, string permission)
    {
        if (user is null || !user.IsAuthenticated || string.IsNullOrEmpty(user.Role))
        {
            return false;
        }

        return RolePermissionMap.Has(user.Role, permission);
    }
}

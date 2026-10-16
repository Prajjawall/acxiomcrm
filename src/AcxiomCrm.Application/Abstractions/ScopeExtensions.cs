using AcxiomCrm.Domain.Common;

namespace AcxiomCrm.Application.Abstractions;

public static class ScopeExtensions
{
    public static IQueryable<T> InScope<T>(this IQueryable<T> q, OwnerScope scope) where T : IOwnedEntity
    {
        if (scope.All)
        {
            return q;
        }

        var ownerIds = scope.OwnerIds.ToArray();
        return q.Where(e => ownerIds.Contains(e.OwnerId));
    }
}

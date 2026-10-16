using AcxiomCrm.Application.Abstractions;
using AcxiomCrm.Domain.Security;
using AcxiomCrm.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AcxiomCrm.Infrastructure.Scope;

public sealed class OwnerScopeResolver(ICurrentUser currentUser, CrmDbContext dbContext) : IOwnerScope
{
    private OwnerScope? _cachedScope;

    public async Task<OwnerScope> ResolveAsync(CancellationToken ct = default)
    {
        if (_cachedScope != null)
        {
            return _cachedScope;
        }

        if (!currentUser.IsAuthenticated || string.IsNullOrWhiteSpace(currentUser.UserId))
        {
            _cachedScope = new OwnerScope(false, []);
            return _cachedScope;
        }

        if (string.Equals(currentUser.Role, Roles.Admin, StringComparison.OrdinalIgnoreCase))
        {
            _cachedScope = new OwnerScope(true, []);
            return _cachedScope;
        }

        if (string.Equals(currentUser.Role, Roles.Manager, StringComparison.OrdinalIgnoreCase))
        {
            var directReportIds = await dbContext.Users
                .AsNoTracking()
                .Where(u => u.ManagerId == currentUser.UserId)
                .Select(u => u.Id)
                .ToListAsync(ct);

            var scopedIds = new HashSet<string>(directReportIds, StringComparer.Ordinal)
            {
                currentUser.UserId
            };

            _cachedScope = new OwnerScope(false, scopedIds);
            return _cachedScope;
        }

        if (string.Equals(currentUser.Role, Roles.SalesExecutive, StringComparison.OrdinalIgnoreCase))
        {
            _cachedScope = new OwnerScope(false, [currentUser.UserId]);
            return _cachedScope;
        }

        // Unknown role -> fail closed
        _cachedScope = new OwnerScope(false, []);
        return _cachedScope;
    }
}

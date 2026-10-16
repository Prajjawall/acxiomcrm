using AcxiomCrm.Application.Abstractions;
using AcxiomCrm.Application.Users;
using AcxiomCrm.Domain.Security;

namespace AcxiomCrm.Infrastructure.Users;

public sealed class UserDirectory : IUserDirectory
{
    private readonly ICrmDbContext _db;
    private readonly ICurrentUser _currentUser;

    public UserDirectory(ICrmDbContext db, ICurrentUser currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public Task<IReadOnlyList<UserOption>> ListAssignableOwnersAsync(CancellationToken ct = default)
    {
        if (!_currentUser.IsAuthenticated || string.IsNullOrWhiteSpace(_currentUser.UserId))
        {
            return Task.FromResult<IReadOnlyList<UserOption>>([]);
        }

        var activeUsers = _db.Users.Where(u => u.IsActive);

        if (string.Equals(_currentUser.Role, Roles.Admin, StringComparison.OrdinalIgnoreCase))
        {
            // Admin: all active Managers and SalesExecutives
            var list = activeUsers
                .Where(u => u.Role == Roles.Manager || u.Role == Roles.SalesExecutive)
                .OrderBy(u => u.FullName)
                .Select(u => new UserOption(u.Id, u.FullName, u.Role))
                .ToList();

            return Task.FromResult<IReadOnlyList<UserOption>>(list);
        }

        if (string.Equals(_currentUser.Role, Roles.Manager, StringComparison.OrdinalIgnoreCase))
        {
            // Manager: self + direct reports (active)
            var list = activeUsers
                .Where(u => u.Id == _currentUser.UserId || u.ManagerId == _currentUser.UserId)
                .OrderBy(u => u.FullName)
                .Select(u => new UserOption(u.Id, u.FullName, u.Role))
                .ToList();

            return Task.FromResult<IReadOnlyList<UserOption>>(list);
        }

        if (string.Equals(_currentUser.Role, Roles.SalesExecutive, StringComparison.OrdinalIgnoreCase))
        {
            // SalesExecutive: self only
            var list = activeUsers
                .Where(u => u.Id == _currentUser.UserId)
                .Select(u => new UserOption(u.Id, u.FullName, u.Role))
                .ToList();

            return Task.FromResult<IReadOnlyList<UserOption>>(list);
        }

        return Task.FromResult<IReadOnlyList<UserOption>>([]);
    }

    public Task<UserOption?> GetOptionAsync(string userId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Task.FromResult<UserOption?>(null);
        }

        var user = _db.Users.FirstOrDefault(u => u.Id == userId);
        if (user == null)
        {
            return Task.FromResult<UserOption?>(null);
        }

        return Task.FromResult<UserOption?>(new UserOption(user.Id, user.FullName, user.Role));
    }
}

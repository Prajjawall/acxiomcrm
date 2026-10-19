using AcxiomCrm.Application.Abstractions;
using AcxiomCrm.Application.Common;
using AcxiomCrm.Application.Users;
using AcxiomCrm.Domain.Enums;
using AcxiomCrm.Domain.Security;
using AcxiomCrm.Domain.Validation;
using AcxiomCrm.Infrastructure.Identity;
using AcxiomCrm.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace AcxiomCrm.Infrastructure.Users;

public sealed class UserAdminService : IUserAdminService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<ApplicationRole> _roleManager;
    private readonly ICrmDbContext _db;
    private readonly ICurrentUser _currentUser;
    private readonly IClock _clock;
    private readonly IAuditWriter _auditWriter;
    private readonly IRefreshTokenRevoker _refreshTokenRevoker;
    private readonly IWorkReassigner _workReassigner;

    public UserAdminService(
        UserManager<ApplicationUser> userManager,
        RoleManager<ApplicationRole> roleManager,
        ICrmDbContext db,
        ICurrentUser currentUser,
        IClock clock,
        IAuditWriter auditWriter,
        IRefreshTokenRevoker refreshTokenRevoker,
        IWorkReassigner workReassigner)
    {
        _userManager = userManager;
        _roleManager = roleManager;
        _db = db;
        _currentUser = currentUser;
        _clock = clock;
        _auditWriter = auditWriter;
        _refreshTokenRevoker = refreshTokenRevoker;
        _workReassigner = workReassigner;
    }

    public async Task<PagedResult<UserListItem>> SearchAsync(UserFilter f, PageQuery p, CancellationToken ct = default)
    {
        if (!_currentUser.IsAuthenticated || !_currentUser.Can(Permissions.UsersView))
        {
            return new PagedResult<UserListItem>([], p.Page, p.PageSize, 0);
        }

        var usersQuery = _userManager.Users.AsNoTracking();

        if (string.Equals(_currentUser.Role, Roles.Manager, StringComparison.OrdinalIgnoreCase))
        {
            usersQuery = usersQuery.Where(u => u.Id == _currentUser.UserId || u.ManagerId == _currentUser.UserId);
        }

        if (!string.IsNullOrWhiteSpace(f.Search))
        {
            var search = f.Search.Trim();
            usersQuery = usersQuery.Where(u =>
                u.FullName.Contains(search) ||
                (u.UserName != null && u.UserName.Contains(search)) ||
                (u.Email != null && u.Email.Contains(search)));
        }

        if (f.Active.HasValue)
        {
            usersQuery = usersQuery.Where(u => u.IsActive == f.Active.Value);
        }

        if (f.LockedOut.HasValue)
        {
            var now = _clock.UtcNow;
            if (f.LockedOut.Value)
            {
                usersQuery = usersQuery.Where(u => u.LockoutEnd != null && u.LockoutEnd > now);
            }
            else
            {
                usersQuery = usersQuery.Where(u => u.LockoutEnd == null || u.LockoutEnd <= now);
            }
        }

        if (!string.IsNullOrWhiteSpace(f.ManagerId))
        {
            usersQuery = usersQuery.Where(u => u.ManagerId == f.ManagerId);
        }

        var crmDb = (CrmDbContext)_db;
        var query = from u in usersQuery
                    join ur in crmDb.UserRoles on u.Id equals ur.UserId into userRoles
                    from ur in userRoles.DefaultIfEmpty()
                    join r in crmDb.Roles on ur.RoleId equals r.Id into roles
                    from r in roles.DefaultIfEmpty()
                    join m in crmDb.Users on u.ManagerId equals m.Id into managers
                    from m in managers.DefaultIfEmpty()
                    select new
                    {
                        User = u,
                        Role = r != null ? r.Name : "",
                        ManagerName = m != null ? m.FullName : null
                    };

        if (!string.IsNullOrWhiteSpace(f.Role))
        {
            query = query.Where(x => x.Role == f.Role);
        }

        var totalCount = await query.CountAsync(ct);

        var nowUtc = _clock.UtcNow;
        var list = await query
            .OrderBy(x => x.User.FullName)
            .Skip(p.Skip)
            .Take(p.Take)
            .Select(x => new
            {
                x.User.Id,
                x.User.FullName,
                UserName = x.User.UserName ?? "",
                Email = x.User.Email ?? "",
                Role = x.Role ?? "",
                x.User.IsActive,
                LockedOut = x.User.LockoutEnd != null && x.User.LockoutEnd > nowUtc,
                LockoutEndUtc = x.User.LockoutEnd.HasValue ? x.User.LockoutEnd.Value.UtcDateTime : (DateTime?)null,
                x.User.ManagerId,
                x.ManagerName,
                LastLoginDateUtc = x.User.LastLoginDate,
                x.User.CreatedDate
            })
            .ToListAsync(ct);

        var items = list.Select(x => new UserListItem(
            x.Id,
            x.FullName,
            x.UserName,
            x.Email,
            x.Role,
            x.IsActive,
            x.LockedOut,
            x.LockoutEndUtc,
            x.ManagerId,
            x.ManagerName,
            x.LastLoginDateUtc,
            x.CreatedDate
        )).ToList();

        return new PagedResult<UserListItem>(items, p.Page, p.PageSize, totalCount);
    }

    public async Task<Result<UserDetail>> GetAsync(string id, CancellationToken ct = default)
    {
        if (!_currentUser.IsAuthenticated || !_currentUser.Can(Permissions.UsersView))
        {
            return Result<UserDetail>.Fail(ErrorKind.Forbidden);
        }

        var user = await _userManager.FindByIdAsync(id);
        if (user == null)
        {
            return Result<UserDetail>.Fail(ErrorKind.NotFound, new FieldError("Id", "User not found."));
        }

        if (string.Equals(_currentUser.Role, Roles.Manager, StringComparison.OrdinalIgnoreCase))
        {
            if (user.Id != _currentUser.UserId && user.ManagerId != _currentUser.UserId)
            {
                return Result<UserDetail>.Fail(ErrorKind.NotFound, new FieldError("Id", "User not found."));
            }
        }

        var roles = await _userManager.GetRolesAsync(user);
        var role = roles.FirstOrDefault() ?? "";

        string? managerName = null;
        if (!string.IsNullOrWhiteSpace(user.ManagerId))
        {
            var manager = await _userManager.FindByIdAsync(user.ManagerId);
            managerName = manager?.FullName;
        }

        var (openLeads, openOpps, plannedFollowUps) = await _workReassigner.GetOpenWorkCountsAsync(user.Id, ct);
        var nowUtc = _clock.UtcNow;

        var detail = new UserDetail(
            user.Id,
            user.FullName,
            user.UserName ?? "",
            user.Email ?? "",
            role,
            user.IsActive,
            user.LockoutEnd != null && user.LockoutEnd > nowUtc,
            user.LockoutEnd.HasValue ? user.LockoutEnd.Value.UtcDateTime : null,
            user.ManagerId,
            managerName,
            user.MustChangePassword,
            user.LastLoginDate,
            user.CreatedDate,
            openLeads,
            openOpps,
            plannedFollowUps
        );

        return Result<UserDetail>.Ok(detail);
    }

    public async Task<Result<CreatedUserResult>> CreateAsync(CreateUserCommand c, CancellationToken ct = default)
    {
        if (!_currentUser.IsAuthenticated || !_currentUser.Can(Permissions.UsersManage))
        {
            return Result<CreatedUserResult>.Fail(ErrorKind.Forbidden);
        }

        if (string.IsNullOrWhiteSpace(c.FullName))
        {
            return Result<CreatedUserResult>.Fail(ErrorKind.Validation, new FieldError("FullName", "Full name is required."));
        }

        if (string.IsNullOrWhiteSpace(c.UserName))
        {
            return Result<CreatedUserResult>.Fail(ErrorKind.Validation, new FieldError("UserName", "Username is required."));
        }

        if (string.IsNullOrWhiteSpace(c.Email) || !c.Email.Contains('@') || !c.Email.Contains('.'))
        {
            return Result<CreatedUserResult>.Fail(ErrorKind.Validation, new FieldError("Email", "Valid email address is required."));
        }

        if (c.Role != Roles.Admin && c.Role != Roles.Manager && c.Role != Roles.SalesExecutive)
        {
            return Result<CreatedUserResult>.Fail(ErrorKind.Validation, new FieldError("Role", "Role is invalid."));
        }

        if (c.Role != Roles.SalesExecutive && !string.IsNullOrWhiteSpace(c.ManagerId))
        {
            return Result<CreatedUserResult>.Fail(ErrorKind.Validation, new FieldError("ManagerId", "Manager is only valid for Sales Executives."));
        }

        if (c.Role == Roles.SalesExecutive && !string.IsNullOrWhiteSpace(c.ManagerId))
        {
            var manager = await _userManager.FindByIdAsync(c.ManagerId);
            if (manager == null || !manager.IsActive || !(await _userManager.IsInRoleAsync(manager, Roles.Manager)))
            {
                return Result<CreatedUserResult>.Fail(ErrorKind.Validation, new FieldError("ManagerId", "Manager must be an active manager."));
            }
        }

        var existingUserByName = await _userManager.FindByNameAsync(c.UserName);
        if (existingUserByName != null)
        {
            return Result<CreatedUserResult>.Fail(ErrorKind.Conflict, new FieldError("UserName", "Username is already taken."));
        }

        var existingUserByEmail = await _userManager.FindByEmailAsync(c.Email);
        if (existingUserByEmail != null)
        {
            return Result<CreatedUserResult>.Fail(ErrorKind.Conflict, new FieldError("Email", "Email address is already in use."));
        }

        var tempPassword = TemporaryPasswordGenerator.Generate(c.UserName, c.Email);

        var user = new ApplicationUser
        {
            FullName = c.FullName.Trim(),
            UserName = c.UserName.Trim(),
            Email = c.Email.Trim(),
            EmailConfirmed = true,
            IsActive = c.IsActive,
            ManagerId = c.Role == Roles.SalesExecutive ? c.ManagerId : null,
            MustChangePassword = true,
            CreatedDate = _clock.UtcNow,
            LockoutEnabled = true
        };

        var createResult = await _userManager.CreateAsync(user, tempPassword);
        if (!createResult.Succeeded)
        {
            var err = createResult.Errors.FirstOrDefault();
            return Result<CreatedUserResult>.Fail(ErrorKind.Validation, new FieldError(err?.Code ?? "Create", err?.Description ?? "Failed to create user."));
        }

        await _userManager.AddToRoleAsync(user, c.Role);

        await _auditWriter.WriteAsync(new AuditEntry("Create", "User", user.Id, AuditResult.Success, Details: $"name={user.FullName};username={user.UserName};role={c.Role}"), ct);

        return Result<CreatedUserResult>.Ok(new CreatedUserResult(user.Id, tempPassword));
    }

    public async Task<Result<UserDetail>> UpdateAsync(string id, UpdateUserCommand c, CancellationToken ct = default)
    {
        if (!_currentUser.IsAuthenticated || !_currentUser.Can(Permissions.UsersManage))
        {
            return Result<UserDetail>.Fail(ErrorKind.Forbidden);
        }

        var user = await _userManager.FindByIdAsync(id);
        if (user == null)
        {
            return Result<UserDetail>.Fail(ErrorKind.NotFound, new FieldError("Id", "User not found."));
        }

        if (string.IsNullOrWhiteSpace(c.FullName))
        {
            return Result<UserDetail>.Fail(ErrorKind.Validation, new FieldError("FullName", "Full name is required."));
        }

        if (string.IsNullOrWhiteSpace(c.UserName))
        {
            return Result<UserDetail>.Fail(ErrorKind.Validation, new FieldError("UserName", "Username is required."));
        }

        if (string.IsNullOrWhiteSpace(c.Email) || !c.Email.Contains('@') || !c.Email.Contains('.'))
        {
            return Result<UserDetail>.Fail(ErrorKind.Validation, new FieldError("Email", "Valid email address is required."));
        }

        if (c.Role != Roles.Admin && c.Role != Roles.Manager && c.Role != Roles.SalesExecutive)
        {
            return Result<UserDetail>.Fail(ErrorKind.Validation, new FieldError("Role", "Role is invalid."));
        }

        if (c.Role != Roles.SalesExecutive && !string.IsNullOrWhiteSpace(c.ManagerId))
        {
            return Result<UserDetail>.Fail(ErrorKind.Validation, new FieldError("ManagerId", "Manager is only valid for Sales Executives."));
        }

        if (c.Role == Roles.SalesExecutive && !string.IsNullOrWhiteSpace(c.ManagerId))
        {
            if (string.Equals(c.ManagerId, user.Id, StringComparison.OrdinalIgnoreCase))
            {
                return Result<UserDetail>.Fail(ErrorKind.Validation, new FieldError("ManagerId", "User cannot be their own manager."));
            }

            var manager = await _userManager.FindByIdAsync(c.ManagerId);
            if (manager == null || !manager.IsActive || !(await _userManager.IsInRoleAsync(manager, Roles.Manager)))
            {
                return Result<UserDetail>.Fail(ErrorKind.Validation, new FieldError("ManagerId", "Manager must be an active manager."));
            }
        }

        var existingUserByName = await _userManager.FindByNameAsync(c.UserName);
        if (existingUserByName != null && existingUserByName.Id != user.Id)
        {
            return Result<UserDetail>.Fail(ErrorKind.Conflict, new FieldError("UserName", "Username is already taken."));
        }

        var existingUserByEmail = await _userManager.FindByEmailAsync(c.Email);
        if (existingUserByEmail != null && existingUserByEmail.Id != user.Id)
        {
            return Result<UserDetail>.Fail(ErrorKind.Conflict, new FieldError("Email", "Email address is already in use."));
        }

        var currentRoles = await _userManager.GetRolesAsync(user);
        var oldRole = currentRoles.FirstOrDefault() ?? "";
        var roleChanging = !string.Equals(oldRole, c.Role, StringComparison.OrdinalIgnoreCase);

        if (roleChanging)
        {
            if (user.Id == _currentUser.UserId)
            {
                return Result<UserDetail>.Fail(ErrorKind.Validation, new FieldError("Role", "Cannot change your own role."));
            }

            if (string.Equals(oldRole, Roles.Admin, StringComparison.OrdinalIgnoreCase))
            {
                var adminUsers = await _userManager.GetUsersInRoleAsync(Roles.Admin);
                var activeAdminCount = adminUsers.Count(u => u.IsActive);
                if (activeAdminCount <= 1)
                {
                    return Result<UserDetail>.Fail(ErrorKind.Validation, new FieldError("Role", "At least one active administrator is required."));
                }
            }

            if (string.Equals(oldRole, Roles.Manager, StringComparison.OrdinalIgnoreCase))
            {
                var hasReports = await _userManager.Users.AnyAsync(u => u.ManagerId == user.Id && u.IsActive, ct);
                if (hasReports)
                {
                    return Result<UserDetail>.Fail(ErrorKind.Validation, new FieldError("Role", "Reassign this manager's team first."));
                }
            }

            await _userManager.RemoveFromRoleAsync(user, oldRole);
            await _userManager.AddToRoleAsync(user, c.Role);
            await _userManager.UpdateSecurityStampAsync(user);
            await _auditWriter.WriteAsync(new AuditEntry("RoleChanged", "User", user.Id, AuditResult.Success, OldValue: oldRole, NewValue: c.Role), ct);
        }

        var nameChanged = user.FullName != c.FullName.Trim();
        var userNameChanged = user.UserName != c.UserName.Trim();
        var emailChanged = user.Email != c.Email.Trim();
        var targetManagerId = c.Role == Roles.SalesExecutive ? c.ManagerId : null;
        var managerChanged = user.ManagerId != targetManagerId;

        user.FullName = c.FullName.Trim();
        user.UserName = c.UserName.Trim();
        user.Email = c.Email.Trim();
        user.ManagerId = targetManagerId;

        var updateResult = await _userManager.UpdateAsync(user);
        if (!updateResult.Succeeded)
        {
            var err = updateResult.Errors.FirstOrDefault();
            return Result<UserDetail>.Fail(ErrorKind.Validation, new FieldError(err?.Code ?? "Update", err?.Description ?? "Failed to update user."));
        }

        if (nameChanged || userNameChanged || emailChanged || managerChanged)
        {
            await _auditWriter.WriteAsync(new AuditEntry("Update", "User", user.Id, AuditResult.Success, Details: $"name={user.FullName};username={user.UserName}"), ct);
        }

        return await GetAsync(user.Id, ct);
    }

    public async Task<Result> ActivateAsync(string id, CancellationToken ct = default)
    {
        if (!_currentUser.IsAuthenticated || !_currentUser.Can(Permissions.UsersManage))
        {
            return Result.Fail(ErrorKind.Forbidden);
        }

        var user = await _userManager.FindByIdAsync(id);
        if (user == null)
        {
            return Result.Fail(ErrorKind.NotFound, new FieldError("Id", "User not found."));
        }

        if (user.IsActive)
        {
            return Result.Ok();
        }

        user.IsActive = true;
        await _userManager.UpdateAsync(user);
        await _auditWriter.WriteAsync(new AuditEntry("Activated", "User", user.Id, AuditResult.Success), ct);
        return Result.Ok();
    }

    public async Task<Result> DeactivateAsync(string id, DeactivateUserCommand c, CancellationToken ct = default)
    {
        if (!_currentUser.IsAuthenticated || !_currentUser.Can(Permissions.UsersManage))
        {
            return Result.Fail(ErrorKind.Forbidden);
        }

        var user = await _userManager.FindByIdAsync(id);
        if (user == null)
        {
            return Result.Fail(ErrorKind.NotFound, new FieldError("Id", "User not found."));
        }

        if (!user.IsActive)
        {
            return Result.Ok();
        }

        if (user.Id == _currentUser.UserId)
        {
            return Result.Fail(ErrorKind.Validation, new FieldError("IsActive", "Cannot deactivate your own account."));
        }

        var userRoles = await _userManager.GetRolesAsync(user);
        if (userRoles.Contains(Roles.Admin))
        {
            var adminUsers = await _userManager.GetUsersInRoleAsync(Roles.Admin);
            var activeAdminCount = adminUsers.Count(u => u.IsActive);
            if (activeAdminCount <= 1)
            {
                return Result.Fail(ErrorKind.Validation, new FieldError("IsActive", "At least one active administrator is required."));
            }
        }

        if (userRoles.Contains(Roles.Manager))
        {
            var hasReports = await _userManager.Users.AnyAsync(u => u.ManagerId == user.Id && u.IsActive, ct);
            if (hasReports)
            {
                return Result.Fail(ErrorKind.Validation, new FieldError("ManagerId", "Reassign this manager's team first."));
            }
        }

        if (!string.IsNullOrWhiteSpace(c?.ReassignToUserId))
        {
            var targetUser = await _userManager.FindByIdAsync(c.ReassignToUserId);
            if (targetUser == null || !targetUser.IsActive)
            {
                return Result.Fail(ErrorKind.Validation, new FieldError("ReassignToUserId", "Reassignment target must be an active user."));
            }

            var count = await _workReassigner.ReassignOpenWorkAsync(user.Id, targetUser.Id, ct);
            await _auditWriter.WriteAsync(new AuditEntry("Assigned", "User", user.Id, AuditResult.Success, Details: $"reassigned={count};target={targetUser.Id}"), ct);
        }

        user.IsActive = false;
        await _userManager.UpdateAsync(user);
        await _userManager.UpdateSecurityStampAsync(user);
        await _refreshTokenRevoker.RevokeAllAsync(user.Id, ct);
        await _auditWriter.WriteAsync(new AuditEntry("Deactivated", "User", user.Id, AuditResult.Success), ct);

        return Result.Ok();
    }

    public async Task<Result> UnlockAsync(string id, CancellationToken ct = default)
    {
        if (!_currentUser.IsAuthenticated || !_currentUser.Can(Permissions.UsersUnlock))
        {
            return Result.Fail(ErrorKind.Forbidden);
        }

        var user = await _userManager.FindByIdAsync(id);
        if (user == null)
        {
            return Result.Fail(ErrorKind.NotFound, new FieldError("Id", "User not found."));
        }

        user.LockoutEnd = null;
        await _userManager.SetLockoutEndDateAsync(user, null);
        await _userManager.ResetAccessFailedCountAsync(user);
        await _userManager.UpdateAsync(user);
        await _auditWriter.WriteAsync(new AuditEntry("Unlock", "User", user.Id, AuditResult.Success), ct);

        return Result.Ok();
    }

    public async Task<Result<string>> ResetPasswordAsync(string id, CancellationToken ct = default)
    {
        if (!_currentUser.IsAuthenticated || !_currentUser.Can(Permissions.UsersResetPassword))
        {
            return Result<string>.Fail(ErrorKind.Forbidden);
        }

        var user = await _userManager.FindByIdAsync(id);
        if (user == null)
        {
            return Result<string>.Fail(ErrorKind.NotFound, new FieldError("Id", "User not found."));
        }

        var tempPassword = TemporaryPasswordGenerator.Generate(user.UserName, user.Email);

        var token = await _userManager.GeneratePasswordResetTokenAsync(user);
        var resetResult = await _userManager.ResetPasswordAsync(user, token, tempPassword);
        if (!resetResult.Succeeded)
        {
            var err = resetResult.Errors.FirstOrDefault();
            return Result<string>.Fail(ErrorKind.Validation, new FieldError(err?.Code ?? "ResetPassword", err?.Description ?? "Failed to reset password."));
        }

        user.MustChangePassword = true;
        user.LockoutEnd = null;
        await _userManager.SetLockoutEndDateAsync(user, null);
        await _userManager.ResetAccessFailedCountAsync(user);
        await _userManager.UpdateSecurityStampAsync(user);
        await _userManager.UpdateAsync(user);

        await _auditWriter.WriteAsync(new AuditEntry("PasswordReset", "User", user.Id, AuditResult.Success, Details: "temporary-password-issued"), ct);

        return Result<string>.Ok(tempPassword);
    }

    public async Task<IReadOnlyList<RoleSummary>> GetRolesAsync(CancellationToken ct = default)
    {
        var adminUsers = await _userManager.GetUsersInRoleAsync(Roles.Admin);
        var managerUsers = await _userManager.GetUsersInRoleAsync(Roles.Manager);
        var execUsers = await _userManager.GetUsersInRoleAsync(Roles.SalesExecutive);

        var list = new List<RoleSummary>
        {
            new(Roles.Admin, "Administrator", adminUsers.Count, RolePermissionMap.For(Roles.Admin).ToList()),
            new(Roles.Manager, "Manager", managerUsers.Count, RolePermissionMap.For(Roles.Manager).ToList()),
            new(Roles.SalesExecutive, "Sales Executive", execUsers.Count, RolePermissionMap.For(Roles.SalesExecutive).ToList())
        };

        return list;
    }
}

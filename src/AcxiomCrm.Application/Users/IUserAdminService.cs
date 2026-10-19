using AcxiomCrm.Application.Common;

namespace AcxiomCrm.Application.Users;

public interface IUserAdminService
{
    Task<PagedResult<UserListItem>> SearchAsync(UserFilter f, PageQuery p, CancellationToken ct = default);
    Task<Result<UserDetail>> GetAsync(string id, CancellationToken ct = default);
    Task<Result<CreatedUserResult>> CreateAsync(CreateUserCommand c, CancellationToken ct = default);
    Task<Result<UserDetail>> UpdateAsync(string id, UpdateUserCommand c, CancellationToken ct = default);
    Task<Result> ActivateAsync(string id, CancellationToken ct = default);
    Task<Result> DeactivateAsync(string id, DeactivateUserCommand c, CancellationToken ct = default);
    Task<Result> UnlockAsync(string id, CancellationToken ct = default);
    Task<Result<string>> ResetPasswordAsync(string id, CancellationToken ct = default);
    Task<IReadOnlyList<RoleSummary>> GetRolesAsync(CancellationToken ct = default);
}

using AcxiomCrm.Application.Common;
using AcxiomCrm.Application.Users;

namespace AcxiomCrm.Web.ViewModels.Users;

public sealed class UserListViewModel
{
    public PagedResult<UserListItem> Result { get; set; } = new([], 1, 25, 0);
    public UserFilter Filter { get; set; } = new();
    public PageQuery PageQuery { get; set; } = new();
    public IReadOnlyList<UserOption> ManagerOptions { get; set; } = [];
}

using AcxiomCrm.Application.Activities;
using AcxiomCrm.Application.Common;
using AcxiomCrm.Application.Users;

namespace AcxiomCrm.Web.ViewModels.Activities;

public sealed class ActivityListViewModel
{
    public PagedResult<ActivityListItem> Result { get; set; } = new([], 1, 25, 0);
    public ActivityFilter Filter { get; set; } = new();
    public PageQuery PageQuery { get; set; } = new();
    public IReadOnlyList<UserOption> AssigneeOptions { get; set; } = [];
}

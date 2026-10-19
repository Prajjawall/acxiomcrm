using AcxiomCrm.Application.Common;
using AcxiomCrm.Application.FollowUps;
using AcxiomCrm.Application.Users;

namespace AcxiomCrm.Web.ViewModels.FollowUps;

public sealed class FollowUpListViewModel
{
    public PagedResult<FollowUpListItem> Result { get; set; } = new([], 1, 25, 0);
    public FollowUpFilter Filter { get; set; } = new();
    public PageQuery PageQuery { get; set; } = new();
    public IReadOnlyList<UserOption> AssigneeOptions { get; set; } = [];
    public string Tab { get; set; } = "pending";
    public NotificationCounts Counts { get; set; } = new(0, 0);
}

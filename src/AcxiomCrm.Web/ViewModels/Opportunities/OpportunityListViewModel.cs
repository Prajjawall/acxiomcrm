using AcxiomCrm.Application.Common;
using AcxiomCrm.Application.Opportunities;
using AcxiomCrm.Application.Users;

namespace AcxiomCrm.Web.ViewModels.Opportunities;

public sealed class OpportunityListViewModel
{
    public PagedResult<OpportunityListItem> Result { get; init; } = new([], 1, 25, 0);
    public OpportunityFilter Filter { get; init; } = new();
    public PageQuery PageQuery { get; init; } = new();
    public IReadOnlyList<UserOption> OwnerOptions { get; init; } = [];
    public string View { get; init; } = "table";
}

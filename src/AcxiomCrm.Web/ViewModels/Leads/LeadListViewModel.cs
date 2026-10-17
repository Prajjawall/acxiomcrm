using AcxiomCrm.Application.Common;
using AcxiomCrm.Application.Leads;
using AcxiomCrm.Application.Users;

namespace AcxiomCrm.Web.ViewModels.Leads;

public sealed class LeadListViewModel
{
    public LeadFilter Filter { get; set; } = new();
    public PageQuery PageQuery { get; set; } = new(1, 20);
    public PagedResult<LeadListItem> Result { get; set; } = new([], 1, 20, 0);
    public IReadOnlyList<UserOption> OwnerOptions { get; set; } = [];
    public bool CanCreate { get; set; }
}

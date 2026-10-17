using AcxiomCrm.Application.Common;
using AcxiomCrm.Application.Customers;
using AcxiomCrm.Application.Users;

namespace AcxiomCrm.Web.ViewModels.Customers;

public sealed class CustomerListViewModel
{
    public CustomerFilter Filter { get; set; } = new();
    public PageQuery PageQuery { get; set; } = new(1, 20);
    public PagedResult<CustomerListItem> Result { get; set; } = new([], 0, 1, 20);
    public IReadOnlyList<UserOption> OwnerOptions { get; set; } = [];
    public bool CanCreate { get; set; }
}

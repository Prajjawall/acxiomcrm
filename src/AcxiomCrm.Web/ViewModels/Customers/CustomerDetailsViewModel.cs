using AcxiomCrm.Application.Audit;
using AcxiomCrm.Application.Customers;

namespace AcxiomCrm.Web.ViewModels.Customers;

public sealed class CustomerDetailsViewModel
{
    public CustomerDetail Customer { get; set; } = default!;
    public IReadOnlyList<AuditListItem> AuditHistory { get; set; } = [];
    public bool CanEdit { get; set; }
    public bool CanDelete { get; set; }
}

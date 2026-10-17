using AcxiomCrm.Application.Audit;
using AcxiomCrm.Application.Customers;
using AcxiomCrm.Application.Leads;

namespace AcxiomCrm.Web.ViewModels.Customers;

public sealed class CustomerDetailsViewModel
{
    public CustomerDetail Customer { get; set; } = default!;
    public IReadOnlyList<AuditListItem> AuditHistory { get; set; } = [];
    public IReadOnlyList<LeadListItem> SourceLeads { get; set; } = [];
    public bool CanEdit { get; set; }
    public bool CanDelete { get; set; }
}

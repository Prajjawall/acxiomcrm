using AcxiomCrm.Application.Audit;
using AcxiomCrm.Application.Leads;
using AcxiomCrm.Application.Users;

namespace AcxiomCrm.Web.ViewModels.Leads;

public sealed class LeadDetailsViewModel
{
    public required LeadDetail Lead { get; set; }
    public IReadOnlyList<AuditListItem> AuditHistory { get; set; } = [];
    public IReadOnlyList<UserOption> AssignableOwners { get; set; } = [];
    public bool CanEdit { get; set; }
    public bool CanDelete { get; set; }
    public bool CanAssign { get; set; }
    public bool CanConvert { get; set; }
}

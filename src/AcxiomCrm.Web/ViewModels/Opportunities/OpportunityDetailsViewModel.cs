using AcxiomCrm.Application.Audit;
using AcxiomCrm.Application.Opportunities;

namespace AcxiomCrm.Web.ViewModels.Opportunities;

public sealed class OpportunityDetailsViewModel
{
    public required OpportunityDetail Detail { get; init; }
    public IReadOnlyList<AuditListItem> History { get; init; } = [];
    public bool CanEdit { get; init; }
    public bool CanDelete { get; init; }
    public bool CanReopen { get; init; }
}

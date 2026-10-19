using AcxiomCrm.Application.Audit;
using AcxiomCrm.Application.FollowUps;

namespace AcxiomCrm.Web.ViewModels.FollowUps;

public sealed class FollowUpDetailsViewModel
{
    public FollowUpDetail Detail { get; set; } = default!;
    public IReadOnlyList<AuditListItem> History { get; set; } = [];
    public bool CanEdit { get; set; }
    public bool CanDelete { get; set; }
    public bool CanComplete { get; set; }
}

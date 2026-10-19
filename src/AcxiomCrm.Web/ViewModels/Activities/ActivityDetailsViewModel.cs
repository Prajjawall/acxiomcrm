using AcxiomCrm.Application.Activities;
using AcxiomCrm.Application.Audit;

namespace AcxiomCrm.Web.ViewModels.Activities;

public sealed class ActivityDetailsViewModel
{
    public ActivityDetail Detail { get; set; } = default!;
    public IReadOnlyList<AuditListItem> History { get; set; } = [];
    public bool CanEdit { get; set; }
    public bool CanDelete { get; set; }
}

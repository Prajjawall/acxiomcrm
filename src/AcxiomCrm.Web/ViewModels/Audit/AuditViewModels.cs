using AcxiomCrm.Application.Audit;
using AcxiomCrm.Application.Common;
using AcxiomCrm.Web.Presentation;

namespace AcxiomCrm.Web.ViewModels.Audit;

public sealed class AuditIndexViewModel
{
    public AuditFilter Filter { get; set; } = new(null, null, null, null, null, null, null, null, null);
    public PagedResult<AuditListItem> Items { get; set; } = new([], 1, 20, 0);
    public PagerModel Pager { get; set; } = null!;
    public IReadOnlyList<string> Modules { get; set; } = [];
}

public sealed class AuditDetailViewModel
{
    public AuditDetail Detail { get; set; } = null!;
}

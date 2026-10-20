using AcxiomCrm.Application.Dashboard;
using AcxiomCrm.Application.Reporting;

namespace AcxiomCrm.Web.ViewModels.Dashboard;

public sealed class DashboardViewModel
{
    public DatePreset Preset { get; init; } = DatePreset.Month;
    public DateOnly? From { get; init; }
    public DateOnly? To { get; init; }
    public IReadOnlyList<KpiCard> Cards { get; init; } = [];
    public IReadOnlyList<LabelValue> LeadStatus { get; init; } = [];
    public IReadOnlyList<StageSeries> Pipeline { get; init; } = [];
    public IReadOnlyList<MonthPoint> MonthlySales { get; init; } = [];
    public decimal WeightedPipeline { get; init; }
    public RolePanels? Panels { get; init; }
    public string UserRole { get; init; } = string.Empty;
}

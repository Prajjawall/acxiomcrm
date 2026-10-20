using AcxiomCrm.Application.Common;
using AcxiomCrm.Application.Reports;
using AcxiomCrm.Domain.Enums;

namespace AcxiomCrm.Web.ViewModels.Reports;

public sealed class CustomerReportViewModel
{
    public CustomerReportFilter Filter { get; init; } = new();
    public PagedResult<CustomerReportItem> Result { get; init; } = new([], 1, 25, 0);
    public string? Search { get; init; }
    public CustomerStatus? Status { get; init; }
}

public sealed class PipelineReportViewModel
{
    public PipelineReportDto Report { get; init; } = new([], [], new(0, 0, 0));
    public DateOnly? From { get; init; }
    public DateOnly? To { get; init; }
}

public sealed class FollowUpReportViewModel
{
    public FollowUpReportFilter Filter { get; init; } = new();
    public PagedResult<FollowUpReportItem> Result { get; init; } = new([], 1, 25, 0);
    public FollowUpStatus? Status { get; init; }
    public bool? Overdue { get; init; }
}

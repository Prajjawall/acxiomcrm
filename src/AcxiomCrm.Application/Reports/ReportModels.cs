using AcxiomCrm.Application.Reporting;
using AcxiomCrm.Domain.Enums;

namespace AcxiomCrm.Application.Reports;

public sealed record PipelineStageSummary(OpportunityStage Stage, int Count, decimal Amount, decimal WeightedAmount);

public sealed record PipelineOwnerSummary(string OwnerId, string OwnerName, int Count, decimal Amount, decimal WeightedAmount);

public sealed record PipelineTotals(int TotalCount, decimal TotalAmount, decimal TotalWeightedAmount);

public sealed record PipelineReportDto(
    IReadOnlyList<PipelineStageSummary> ByStage,
    IReadOnlyList<PipelineOwnerSummary> ByOwner,
    PipelineTotals Totals);

public sealed record CustomerReportItem(
    int Id,
    string Code,
    string Name,
    string? CompanyName,
    string Email,
    string Phone,
    CustomerStatus Status,
    string OwnerName,
    DateTime CreatedDate);

public sealed record CustomerReportFilter(
    string? Search = null,
    CustomerStatus? Status = null,
    string? OwnerId = null,
    DateRange? Range = null);

public sealed record FollowUpReportItem(
    int Id,
    RelatedType RelatedType,
    string RelatedName,
    string Subject,
    FollowUpType Type,
    FollowUpStatus Status,
    DateTime FollowUpDateUtc,
    string AssignedToName,
    bool IsOverdue);

public sealed record FollowUpReportFilter(
    FollowUpStatus? Status = null,
    bool? Overdue = null,
    string? AssignedToId = null,
    DateRange? Range = null);

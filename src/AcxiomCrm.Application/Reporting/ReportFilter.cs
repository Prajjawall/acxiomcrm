namespace AcxiomCrm.Application.Reporting;

public sealed record ReportFilter(
    DateRange Range,
    string? OwnerId = null,
    string? Status = null,
    string? Stage = null,
    string? Source = null,
    string? Search = null);

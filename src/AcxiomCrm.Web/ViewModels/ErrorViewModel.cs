namespace AcxiomCrm.Web.ViewModels;

public sealed record ErrorViewModel(
    string? CorrelationId,
    int StatusCode,
    string? Message = null);

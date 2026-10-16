namespace AcxiomCrm.Application.Audit;

public sealed record AuditDetail(
    AuditListItem Header,
    IReadOnlyList<AuditChange> Changes,
    string? CorrelationId);

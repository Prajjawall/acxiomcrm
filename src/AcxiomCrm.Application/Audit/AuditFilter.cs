using AcxiomCrm.Domain.Enums;

namespace AcxiomCrm.Application.Audit;

public sealed record AuditFilter(
    string? UserId = null,
    string? Module = null,
    string? Action = null,
    AuditResult? Result = null,
    DateOnly? From = null,
    DateOnly? To = null,
    string? EntityName = null,
    string? RecordId = null,
    string? Text = null);

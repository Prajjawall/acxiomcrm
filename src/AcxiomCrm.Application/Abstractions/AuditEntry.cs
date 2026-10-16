using AcxiomCrm.Domain.Enums;

namespace AcxiomCrm.Application.Abstractions;

public sealed record AuditEntry(
    string Action,
    string EntityName,
    string? RecordId,
    AuditResult Result,
    string? OldValue = null,
    string? NewValue = null,
    string? Details = null,
    string? UserIdOverride = null);

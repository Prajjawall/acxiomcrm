using AcxiomCrm.Domain.Enums;

namespace AcxiomCrm.Application.Audit;

public sealed record AuditListItem(
    long Id,
    DateTime CreatedDateUtc,
    string? UserName,
    string Module,
    string Action,
    string EntityName,
    string? RecordId,
    AuditResult Result,
    string? IpAddress,
    string? Details);

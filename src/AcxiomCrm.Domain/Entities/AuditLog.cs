using AcxiomCrm.Domain.Enums;

namespace AcxiomCrm.Domain.Entities;

public class AuditLog
{
    public long AuditLogId { get; set; }
    public DateTime CreatedDate { get; set; }
    public string? UserId { get; set; }
    public string? UserName { get; set; }
    public string Module { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string EntityName { get; set; } = string.Empty;
    public string? RecordId { get; set; }
    public AuditResult Result { get; set; } = AuditResult.Success;
    public string? OldValue { get; set; }
    public string? NewValue { get; set; }
    public string? Details { get; set; }
    public string? IpAddress { get; set; }
    public string? CorrelationId { get; set; }
}

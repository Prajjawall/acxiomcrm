namespace AcxiomCrm.Application.Abstractions;

public interface IAuditWriter
{
    Task WriteAsync(AuditEntry entry, CancellationToken ct = default);
}

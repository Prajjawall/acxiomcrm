namespace AcxiomCrm.Application.Audit;

public sealed record AuditChange(string Property, string? Old, string? New);

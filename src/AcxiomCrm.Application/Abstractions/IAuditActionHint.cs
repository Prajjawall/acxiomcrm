using System.Diagnostics.CodeAnalysis;

namespace AcxiomCrm.Application.Abstractions;

[SuppressMessage("Naming", "CA1716:Identifiers should not match keywords", Justification = "Contract defined in plan")]
public interface IAuditActionHint
{
    void Set(string action);
    string? Take();
}

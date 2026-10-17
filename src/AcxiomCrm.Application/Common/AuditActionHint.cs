using AcxiomCrm.Application.Abstractions;

namespace AcxiomCrm.Application.Common;

public sealed class AuditActionHint : IAuditActionHint
{
    private string? _action;

    public void Set(string action)
    {
        _action = action;
    }

    public string? Take()
    {
        var action = _action;
        _action = null;
        return action;
    }
}

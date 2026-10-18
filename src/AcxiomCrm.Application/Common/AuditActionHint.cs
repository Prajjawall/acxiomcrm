using AcxiomCrm.Application.Abstractions;

namespace AcxiomCrm.Application.Common;

public sealed class AuditActionHint : IAuditActionHint
{
    private string? _action;
    private string? _details;

    public void Set(string action)
    {
        _action = action;
        _details = null;
    }

    public void Set(string action, string? details)
    {
        _action = action;
        _details = details;
    }

    public string? Take()
    {
        var action = _action;
        _action = null;
        return action;
    }

    public string? TakeDetails()
    {
        var details = _details;
        _details = null;
        return details;
    }
}

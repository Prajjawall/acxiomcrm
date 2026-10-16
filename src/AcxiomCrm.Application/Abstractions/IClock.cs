namespace AcxiomCrm.Application.Abstractions;

public interface IClock
{
    DateTime UtcNow { get; }
    DateOnly Today { get; }
    DateTime ToBusinessLocal(DateTime utc);
}

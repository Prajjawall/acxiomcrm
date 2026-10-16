namespace AcxiomCrm.Application.Abstractions;

public interface IOwnerScope
{
    Task<OwnerScope> ResolveAsync(CancellationToken ct = default);
}

namespace AcxiomCrm.Application.Abstractions;

public sealed record OwnerScope(bool All, IReadOnlyCollection<string> OwnerIds)
{
    public bool Contains(string ownerId)
    {
        if (All)
        {
            return true;
        }

        if (ownerId == null)
        {
            return false;
        }

        return OwnerIds.Contains(ownerId, StringComparer.Ordinal);
    }
}

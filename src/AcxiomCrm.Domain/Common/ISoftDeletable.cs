namespace AcxiomCrm.Domain.Common;

public interface ISoftDeletable
{
    bool IsDeleted { get; }
    DateTime? DeletedDate { get; }
    string? DeletedBy { get; }
}

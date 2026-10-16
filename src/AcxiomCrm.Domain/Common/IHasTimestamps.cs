namespace AcxiomCrm.Domain.Common;

public interface IHasTimestamps
{
    DateTime CreatedDate { get; }
    string CreatedBy { get; }
    DateTime? ModifiedDate { get; }
    string? ModifiedBy { get; }
}

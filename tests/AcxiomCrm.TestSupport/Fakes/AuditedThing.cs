using AcxiomCrm.Domain.Common;

namespace AcxiomCrm.TestSupport.Fakes;

public class AuditedThing : IAuditedEntity, IHasTimestamps, ISoftDeletable, IOwnedEntity
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? PasswordHash { get; set; }
    public byte[]? RowVersion { get; set; }

    public DateTime CreatedDate { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
    public DateTime? ModifiedDate { get; set; }
    public string? ModifiedBy { get; set; }

    public bool IsDeleted { get; set; }
    public DateTime? DeletedDate { get; set; }
    public string? DeletedBy { get; set; }

    public string OwnerId { get; set; } = string.Empty;
}

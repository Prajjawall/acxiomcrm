using AcxiomCrm.Domain.Enums;

namespace AcxiomCrm.Application.Common;

public sealed record RelatedRef(RelatedType Type, int Id);

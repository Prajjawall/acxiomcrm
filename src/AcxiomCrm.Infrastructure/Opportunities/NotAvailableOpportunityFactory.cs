using AcxiomCrm.Application.Common;
using AcxiomCrm.Application.Leads;
using AcxiomCrm.Domain.Validation;

namespace AcxiomCrm.Infrastructure.Opportunities;

public sealed class NotAvailableOpportunityFactory : IOpportunityFactory
{
    public bool IsAvailable => false;

    public Task<Result<int>> CreateForConversionAsync(
        int customerId,
        int leadId,
        string ownerId,
        ConvertOpportunityInput input,
        CancellationToken ct = default)
    {
        return Task.FromResult(Result<int>.Fail(ErrorKind.Validation, [new FieldError("Opportunity", "Opportunities are not enabled yet.")]));
    }
}

using AcxiomCrm.Application.Common;

namespace AcxiomCrm.Application.Leads;

public interface IOpportunityFactory
{
    bool IsAvailable { get; }

    Task<Result<int>> CreateForConversionAsync(
        int customerId,
        int leadId,
        string ownerId,
        ConvertOpportunityInput input,
        CancellationToken ct = default);
}

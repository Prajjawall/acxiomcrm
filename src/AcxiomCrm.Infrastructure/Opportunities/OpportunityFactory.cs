using AcxiomCrm.Application.Abstractions;
using AcxiomCrm.Application.Common;
using AcxiomCrm.Application.Leads;
using AcxiomCrm.Domain.Entities;
using AcxiomCrm.Domain.Enums;
using AcxiomCrm.Domain.Rules;
using AcxiomCrm.Domain.Validation;
using AcxiomCrm.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AcxiomCrm.Infrastructure.Opportunities;

public sealed class OpportunityFactory : IOpportunityFactory
{
    private readonly CrmDbContext _db;
    private readonly IClock _clock;

    public OpportunityFactory(CrmDbContext db, IClock clock)
    {
        _db = db;
        _clock = clock;
    }

    public bool IsAvailable => true;

    public async Task<Result<int>> CreateForConversionAsync(
        int customerId,
        int leadId,
        string ownerId,
        ConvertOpportunityInput input,
        CancellationToken ct = default)
    {
        var validationErrors = OpportunityRules.Validate(
            input.Name,
            input.Amount,
            input.Probability,
            input.ExpectedCloseDate,
            OpportunityStage.Qualification,
            _clock.Today,
            input.Notes,
            null);

        if (validationErrors.Count > 0)
        {
            return Result<int>.Fail(ErrorKind.Validation, validationErrors);
        }

        var customer = await _db.Customers.FirstOrDefaultAsync(c => c.Id == customerId, ct);
        if (customer == null || customer.Status != CustomerStatus.Active)
        {
            return Result<int>.Fail(ErrorKind.Validation, [new FieldError("CustomerId", "Choose an active customer.")]);
        }

        var opp = Opportunity.Create(
            input.Name,
            customerId,
            input.Amount,
            OpportunityStage.Qualification,
            input.Probability,
            input.ExpectedCloseDate,
            ownerId,
            leadId: leadId,
            source: null,
            notes: input.Notes);

        _db.Opportunities.Add(opp);
        await _db.SaveChangesAsync(ct);
        return Result<int>.Ok(opp.Id);
    }
}

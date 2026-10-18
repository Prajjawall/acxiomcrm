using AcxiomCrm.Application.Abstractions;
using AcxiomCrm.Application.Common;
using AcxiomCrm.Application.Opportunities;
using AcxiomCrm.Application.Users;
using AcxiomCrm.Domain.Audit;
using AcxiomCrm.Domain.Entities;
using AcxiomCrm.Domain.Enums;
using AcxiomCrm.Domain.Rules;
using AcxiomCrm.Domain.Security;
using AcxiomCrm.Domain.Validation;
using AcxiomCrm.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AcxiomCrm.Infrastructure.Opportunities;

public sealed class OpportunityService : IOpportunityService
{
    private readonly CrmDbContext _db;
    private readonly ICurrentUser _currentUser;
    private readonly IOwnerScope _ownerScope;
    private readonly IClock _clock;
    private readonly IUserDirectory _userDirectory;
    private readonly IAuditActionHint _actionHint;

    public OpportunityService(
        CrmDbContext db,
        ICurrentUser currentUser,
        IOwnerScope ownerScope,
        IClock clock,
        IUserDirectory userDirectory,
        IAuditActionHint actionHint)
    {
        _db = db;
        _currentUser = currentUser;
        _ownerScope = ownerScope;
        _clock = clock;
        _userDirectory = userDirectory;
        _actionHint = actionHint;
    }

    public async Task<PagedResult<OpportunityListItem>> SearchAsync(OpportunityFilter f, PageQuery p, CancellationToken ct = default)
    {
        if (!_currentUser.Can(Permissions.OpportunitiesView))
        {
            return new PagedResult<OpportunityListItem>([], p.Page, p.PageSize, 0);
        }

        var scope = await _ownerScope.ResolveAsync(ct);
        var query = _db.Opportunities.AsNoTracking().InScope(scope).Include(o => o.Customer).AsQueryable();

        if (f.CustomerId.HasValue)
        {
            query = query.Where(o => o.CustomerId == f.CustomerId.Value);
        }

        if (f.Stage.HasValue)
        {
            query = query.Where(o => o.Stage == f.Stage.Value);
        }

        if (f.Status.HasValue)
        {
            query = query.Where(o => o.Status == f.Status.Value);
        }

        if (!string.IsNullOrWhiteSpace(f.OwnerId))
        {
            query = query.Where(o => o.OwnerId == f.OwnerId);
        }

        var normTerm = SearchHelper.Normalise(f.Search);
        if (normTerm is not null)
        {
            query = query.Where(o => o.Name.Contains(normTerm) || (o.Customer != null && o.Customer.Name.Contains(normTerm)));
        }

        var total = await query.CountAsync(ct);

        query = (p.Sort?.ToLowerInvariant()) switch
        {
            "name" => p.Desc ? query.OrderByDescending(o => o.Name) : query.OrderBy(o => o.Name),
            "amount" => p.Desc ? query.OrderByDescending(o => o.Amount) : query.OrderBy(o => o.Amount),
            "weighted" => p.Desc ? query.OrderByDescending(o => o.Amount * o.Probability) : query.OrderBy(o => o.Amount * o.Probability),
            "expectedclosedate" or "close" or "date" => p.Desc ? query.OrderByDescending(o => o.ExpectedCloseDate) : query.OrderBy(o => o.ExpectedCloseDate),
            "stage" => p.Desc ? query.OrderByDescending(o => o.Stage) : query.OrderBy(o => o.Stage),
            _ => p.Desc ? query.OrderByDescending(o => o.CreatedDate) : query.OrderBy(o => o.CreatedDate)
        };

        var page = Math.Max(1, p.Page);
        var pageSize = Math.Clamp(p.PageSize, 1, 100);
        var items = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);

        var ownerIds = items.Select(x => x.OwnerId).Distinct().ToList();
        var ownerNames = new Dictionary<string, string>();
        foreach (var oid in ownerIds)
        {
            var opt = await _userDirectory.GetOptionAsync(oid, ct);
            ownerNames[oid] = opt?.Name ?? oid;
        }

        var mapped = items.Select(o => new OpportunityListItem(
            o.Id,
            o.Name,
            o.CustomerId,
            o.Customer?.Name ?? string.Empty,
            o.Customer?.Code ?? string.Empty,
            o.Amount,
            o.Probability,
            OpportunityRules.Weighted(o.Amount, o.Probability),
            o.Stage,
            o.Status,
            o.ExpectedCloseDate,
            o.Status == OpportunityStatus.Open && o.ExpectedCloseDate < _clock.Today,
            o.OwnerId,
            ownerNames.GetValueOrDefault(o.OwnerId, o.OwnerId)
        )).ToList();

        return new PagedResult<OpportunityListItem>(mapped, page, pageSize, total);
    }

    public async Task<Result<OpportunityDetail>> GetAsync(int id, CancellationToken ct = default)
    {
        if (!_currentUser.Can(Permissions.OpportunitiesView))
        {
            return Result<OpportunityDetail>.Fail(ErrorKind.Forbidden);
        }

        var scope = await _ownerScope.ResolveAsync(ct);
        var opp = await _db.Opportunities.AsNoTracking()
            .InScope(scope)
            .Include(o => o.Customer)
            .FirstOrDefaultAsync(o => o.Id == id, ct);

        if (opp == null)
        {
            return Result<OpportunityDetail>.Fail(ErrorKind.NotFound);
        }

        var owner = await _userDirectory.GetOptionAsync(opp.OwnerId, ct);
        return Result<OpportunityDetail>.Ok(MapToDetail(opp, owner?.Name ?? opp.OwnerId));
    }

    public async Task<Result<OpportunityDetail>> CreateAsync(CreateOpportunityCommand c, CancellationToken ct = default)
    {
        if (!_currentUser.Can(Permissions.OpportunitiesCreate))
        {
            return Result<OpportunityDetail>.Fail(ErrorKind.Forbidden);
        }

        if (!OpportunityRules.IsOpen(c.Stage))
        {
            return Result<OpportunityDetail>.Fail(ErrorKind.Validation, [new FieldError("Stage", "Stage must be an open stage.")]);
        }

        var scope = await _ownerScope.ResolveAsync(ct);
        var customer = await _db.Customers.InScope(scope).FirstOrDefaultAsync(x => x.Id == c.CustomerId, ct);
        if (customer == null || customer.Status != CustomerStatus.Active)
        {
            return Result<OpportunityDetail>.Fail(ErrorKind.Validation, [new FieldError("CustomerId", "Choose an active customer.")]);
        }

        string targetOwnerId;
        if (_currentUser.Role == Roles.SalesExecutive)
        {
            targetOwnerId = _currentUser.UserId ?? string.Empty;
        }
        else
        {
            targetOwnerId = !string.IsNullOrWhiteSpace(c.OwnerId) ? c.OwnerId : (_currentUser.UserId ?? string.Empty);
        }

        var errors = OpportunityRules.Validate(c.Name, c.Amount, c.Probability, c.ExpectedCloseDate, c.Stage, _clock.Today, c.Notes, null);
        if (errors.Count > 0)
        {
            return Result<OpportunityDetail>.Fail(ErrorKind.Validation, errors);
        }

        var opp = Opportunity.Create(
            c.Name,
            c.CustomerId,
            c.Amount,
            c.Stage,
            (byte)c.Probability,
            c.ExpectedCloseDate,
            targetOwnerId,
            leadId: null,
            source: c.Source,
            notes: c.Notes);

        _db.Opportunities.Add(opp);
        await _db.SaveChangesAsync(ct);

        return await GetAsync(opp.Id, ct);
    }

    public async Task<Result<OpportunityDetail>> UpdateAsync(int id, UpdateOpportunityCommand c, CancellationToken ct = default)
    {
        var scope = await _ownerScope.ResolveAsync(ct);
        var opp = await _db.Opportunities.InScope(scope).FirstOrDefaultAsync(o => o.Id == id, ct);
        if (opp == null)
        {
            return Result<OpportunityDetail>.Fail(ErrorKind.NotFound);
        }

        if (!_currentUser.Can(Permissions.OpportunitiesEdit))
        {
            return Result<OpportunityDetail>.Fail(ErrorKind.Forbidden);
        }

        if (opp.Status != OpportunityStatus.Open)
        {
            return Result<OpportunityDetail>.Fail(ErrorKind.Conflict, [new FieldError(string.Empty, "This opportunity is closed and cannot be edited.")]);
        }

        var customer = await _db.Customers.InScope(scope).FirstOrDefaultAsync(x => x.Id == c.CustomerId, ct);
        if (customer == null || customer.Status != CustomerStatus.Active)
        {
            return Result<OpportunityDetail>.Fail(ErrorKind.Validation, [new FieldError("CustomerId", "Choose an active customer.")]);
        }

        string targetOwnerId;
        if (_currentUser.Role == Roles.SalesExecutive)
        {
            targetOwnerId = opp.OwnerId;
        }
        else
        {
            targetOwnerId = !string.IsNullOrWhiteSpace(c.OwnerId) ? c.OwnerId : opp.OwnerId;
        }

        var errors = OpportunityRules.Validate(c.Name, c.Amount, c.Probability, c.ExpectedCloseDate, opp.Stage, _clock.Today, c.Notes, null);
        if (errors.Count > 0)
        {
            return Result<OpportunityDetail>.Fail(ErrorKind.Validation, errors);
        }

        if (c.RowVersion is not null && c.RowVersion.Length > 0)
        {
            _db.Entry(opp).Property(x => x.RowVersion).OriginalValue = c.RowVersion;
        }

        opp.UpdateDetails(c.Name, c.Amount, c.ExpectedCloseDate, (byte)c.Probability, c.Source, c.Notes);
        opp.AssignTo(targetOwnerId);

        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            return ConcurrencyHelper.Conflict<OpportunityDetail>("Opportunity");
        }

        return await GetAsync(opp.Id, ct);
    }

    public async Task<Result> DeleteAsync(int id, CancellationToken ct = default)
    {
        var scope = await _ownerScope.ResolveAsync(ct);
        var opp = await _db.Opportunities.InScope(scope).FirstOrDefaultAsync(o => o.Id == id, ct);
        if (opp == null)
        {
            return Result.Fail(ErrorKind.NotFound);
        }

        if (!_currentUser.Can(Permissions.OpportunitiesDelete))
        {
            return Result.Fail(ErrorKind.Forbidden);
        }

        _db.Opportunities.Remove(opp);
        await _db.SaveChangesAsync(ct);
        return Result.Ok();
    }

    public async Task<Result<OpportunityDetail>> ChangeStageAsync(int id, OpportunityStage targetStage, string? closeNote, byte[] rowVersion, CancellationToken ct = default)
    {
        var scope = await _ownerScope.ResolveAsync(ct);
        var opp = await _db.Opportunities.InScope(scope).FirstOrDefaultAsync(o => o.Id == id, ct);
        if (opp == null)
        {
            return Result<OpportunityDetail>.Fail(ErrorKind.NotFound);
        }

        if (!_currentUser.Can(Permissions.OpportunitiesEdit))
        {
            return Result<OpportunityDetail>.Fail(ErrorKind.Forbidden);
        }

        if (opp.Status != OpportunityStatus.Open)
        {
            return Result<OpportunityDetail>.Fail(ErrorKind.Conflict, [new FieldError(string.Empty, "This opportunity is closed and cannot change stage.")]);
        }

        if (targetStage == OpportunityStage.Won)
        {
            if (string.IsNullOrWhiteSpace(closeNote))
            {
                return Result<OpportunityDetail>.Fail(ErrorKind.Validation, [new FieldError("CloseNote", "Enter a note about why this opportunity was won.")]);
            }
            if (closeNote.Length > Limits.Notes)
            {
                return Result<OpportunityDetail>.Fail(ErrorKind.Validation, [new FieldError("CloseNote", Messages.MaxLength("Close note", Limits.Notes))]);
            }
        }
        else if (targetStage == OpportunityStage.Lost)
        {
            if (string.IsNullOrWhiteSpace(closeNote))
            {
                return Result<OpportunityDetail>.Fail(ErrorKind.Validation, [new FieldError("CloseNote", "Enter the reason this opportunity was lost.")]);
            }
            if (closeNote.Length > Limits.Notes)
            {
                return Result<OpportunityDetail>.Fail(ErrorKind.Validation, [new FieldError("CloseNote", Messages.MaxLength("Close note", Limits.Notes))]);
            }
        }
        else if (OpportunityRules.IsOpen(targetStage) && opp.ExpectedCloseDate < _clock.Today)
        {
            return Result<OpportunityDetail>.Fail(ErrorKind.Validation, [new FieldError("ExpectedCloseDate", Messages.ExpectedCloseDatePast)]);
        }

        if (rowVersion is not null && rowVersion.Length > 0)
        {
            _db.Entry(opp).Property(x => x.RowVersion).OriginalValue = rowVersion;
        }

        _actionHint.Set(AuditActions.StageChanged);
        opp.MoveTo(targetStage, _clock.Today, closeNote);

        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            return ConcurrencyHelper.Conflict<OpportunityDetail>("Opportunity");
        }

        return await GetAsync(opp.Id, ct);
    }

    public async Task<Result<OpportunityDetail>> ReopenAsync(int id, byte[] rowVersion, CancellationToken ct = default)
    {
        var scope = await _ownerScope.ResolveAsync(ct);
        var opp = await _db.Opportunities.InScope(scope).FirstOrDefaultAsync(o => o.Id == id, ct);
        if (opp == null)
        {
            return Result<OpportunityDetail>.Fail(ErrorKind.NotFound);
        }

        if (!_currentUser.Can(Permissions.OpportunitiesReopen))
        {
            return Result<OpportunityDetail>.Fail(ErrorKind.Forbidden);
        }

        if (opp.Status == OpportunityStatus.Open)
        {
            return Result<OpportunityDetail>.Fail(ErrorKind.Conflict, [new FieldError(string.Empty, "Opportunity is not closed.")]);
        }

        if (rowVersion is not null && rowVersion.Length > 0)
        {
            _db.Entry(opp).Property(x => x.RowVersion).OriginalValue = rowVersion;
        }

        _actionHint.Set(AuditActions.StageChanged, "reopened");
        opp.Reopen();

        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            return ConcurrencyHelper.Conflict<OpportunityDetail>("Opportunity");
        }

        return await GetAsync(opp.Id, ct);
    }

    public async Task<IReadOnlyList<BoardColumn>> GetBoardAsync(OpportunityFilter f, int maxCardsPerColumn = 50, CancellationToken ct = default)
    {
        if (!_currentUser.Can(Permissions.OpportunitiesView))
        {
            return [];
        }

        var scope = await _ownerScope.ResolveAsync(ct);
        var baseQuery = _db.Opportunities.AsNoTracking().InScope(scope).Include(o => o.Customer).AsQueryable();

        if (f.CustomerId.HasValue)
        {
            baseQuery = baseQuery.Where(o => o.CustomerId == f.CustomerId.Value);
        }

        if (!string.IsNullOrWhiteSpace(f.OwnerId))
        {
            baseQuery = baseQuery.Where(o => o.OwnerId == f.OwnerId);
        }

        var normTerm = SearchHelper.Normalise(f.Search);
        if (normTerm is not null)
        {
            baseQuery = baseQuery.Where(o => o.Name.Contains(normTerm) || (o.Customer != null && o.Customer.Name.Contains(normTerm)));
        }

        var thirtyDaysAgo = _clock.Today.AddDays(-30);
        baseQuery = baseQuery.Where(o => o.Status == OpportunityStatus.Open || (o.ClosedDate.HasValue && o.ClosedDate.Value >= thirtyDaysAgo));

        var stages = new[]
        {
            OpportunityStage.Qualification,
            OpportunityStage.Proposal,
            OpportunityStage.Negotiation,
            OpportunityStage.Won,
            OpportunityStage.Lost
        };

        var allForTotals = await baseQuery.Select(o => new { o.Stage, o.Amount, o.Probability }).ToListAsync(ct);
        var grouped = allForTotals.GroupBy(x => x.Stage).ToDictionary(
            g => g.Key,
            g => (
                Count: g.Count(),
                Amount: g.Sum(x => x.Amount),
                Weighted: g.Sum(x => OpportunityRules.Weighted(x.Amount, x.Probability))
            )
        );

        var allCards = await baseQuery
            .OrderByDescending(o => o.CreatedDate)
            .ToListAsync(ct);

        var ownerIds = allCards.Select(c => c.OwnerId).Distinct().ToList();
        var ownerNames = new Dictionary<string, string>();
        foreach (var oid in ownerIds)
        {
            var opt = await _userDirectory.GetOptionAsync(oid, ct);
            ownerNames[oid] = opt?.Name ?? oid;
        }

        var result = new List<BoardColumn>();
        foreach (var stage in stages)
        {
            var stats = grouped.GetValueOrDefault(stage, (Count: 0, Amount: 0m, Weighted: 0m));
            var stageCards = allCards
                .Where(c => c.Stage == stage)
                .Take(maxCardsPerColumn)
                .Select(o => new OpportunityListItem(
                    o.Id,
                    o.Name,
                    o.CustomerId,
                    o.Customer?.Name ?? string.Empty,
                    o.Customer?.Code ?? string.Empty,
                    o.Amount,
                    o.Probability,
                    OpportunityRules.Weighted(o.Amount, o.Probability),
                    o.Stage,
                    o.Status,
                    o.ExpectedCloseDate,
                    o.Status == OpportunityStatus.Open && o.ExpectedCloseDate < _clock.Today,
                    o.OwnerId,
                    ownerNames.GetValueOrDefault(o.OwnerId, o.OwnerId)
                ))
                .ToList();

            result.Add(new BoardColumn(stage, stats.Count, stats.Amount, stats.Weighted, stageCards));
        }

        return result;
    }

    private static OpportunityDetail MapToDetail(Opportunity opp, string ownerName)
    {
        return new OpportunityDetail(
            opp.Id,
            opp.Name,
            opp.CustomerId,
            opp.Customer?.Name ?? string.Empty,
            opp.Customer?.Code ?? string.Empty,
            opp.LeadId,
            opp.Amount,
            opp.Probability,
            OpportunityRules.Weighted(opp.Amount, opp.Probability),
            opp.Stage,
            opp.Status,
            opp.ExpectedCloseDate,
            opp.ClosedDate,
            opp.CloseNote,
            opp.Source,
            opp.OwnerId,
            ownerName,
            opp.Notes,
            opp.CreatedDate,
            opp.ModifiedDate,
            opp.RowVersion
        );
    }
}

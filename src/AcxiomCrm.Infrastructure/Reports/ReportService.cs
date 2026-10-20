using AcxiomCrm.Application.Abstractions;
using AcxiomCrm.Application.Common;
using AcxiomCrm.Application.Reporting;
using AcxiomCrm.Application.Reports;
using AcxiomCrm.Domain.Enums;
using AcxiomCrm.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AcxiomCrm.Infrastructure.Reports;

public sealed class ReportService : IReportService
{
    private readonly CrmDbContext _db;
    private readonly IOwnerScope _ownerScope;
    private readonly ICurrentUser _currentUser;
    private readonly IClock _clock;

    public ReportService(
        CrmDbContext db,
        IOwnerScope ownerScope,
        ICurrentUser currentUser,
        IClock clock)
    {
        _db = db;
        _ownerScope = ownerScope;
        _currentUser = currentUser;
        _clock = clock;
    }

    public async Task<Result<PipelineReportDto>> GetPipelineAsync(ReportFilter filter, CancellationToken ct = default)
    {
        var scope = await _ownerScope.ResolveAsync(ct);
        var query = _db.Opportunities.AsNoTracking().InScope(scope)
            .Where(o => o.Status == OpportunityStatus.Open);

        if (!string.IsNullOrEmpty(filter.OwnerId))
        {
            if (scope.All || scope.OwnerIds.Contains(filter.OwnerId))
            {
                query = query.Where(o => o.OwnerId == filter.OwnerId);
            }
        }

        var opps = await query
            .Select(o => new
            {
                o.Stage,
                o.OwnerId,
                o.Amount,
                o.Probability
            })
            .ToListAsync(ct);

        // Map owners to names from directory/users
        var ownerIds = opps.Select(o => o.OwnerId).Distinct().ToList();
        var userNames = await _db.Users.AsNoTracking()
            .Where(u => ownerIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.FullName, ct);

        // Stage summary
        var allStages = new[]
        {
            OpportunityStage.Qualification,
            OpportunityStage.Proposal,
            OpportunityStage.Negotiation
        };

        var stageSummaries = allStages.Select(stage =>
        {
            var stageOpps = opps.Where(o => o.Stage == stage).ToList();
            var count = stageOpps.Count;
            var amount = stageOpps.Sum(o => o.Amount);
            var weighted = stageOpps.Sum(o => Math.Round(o.Amount * o.Probability / 100m, 2));
            return new PipelineStageSummary(stage, count, amount, weighted);
        }).ToList();

        // Owner summary
        var ownerSummaries = opps.GroupBy(o => o.OwnerId)
            .Select(g =>
            {
                var name = userNames.TryGetValue(g.Key, out var n) ? n : g.Key;
                var count = g.Count();
                var amount = g.Sum(o => o.Amount);
                var weighted = g.Sum(o => Math.Round(o.Amount * o.Probability / 100m, 2));
                return new PipelineOwnerSummary(g.Key, name, count, amount, weighted);
            })
            .OrderBy(o => o.OwnerName)
            .ToList();

        var totalCount = opps.Count;
        var totalAmount = opps.Sum(o => o.Amount);
        var totalWeighted = opps.Sum(o => Math.Round(o.Amount * o.Probability / 100m, 2));
        var totals = new PipelineTotals(totalCount, totalAmount, totalWeighted);

        return Result<PipelineReportDto>.Ok(new PipelineReportDto(stageSummaries, ownerSummaries, totals));
    }

    public async Task<PagedResult<CustomerReportItem>> RunCustomerReportAsync(CustomerReportFilter filter, PageQuery pageQuery, CancellationToken ct = default)
    {
        var scope = await _ownerScope.ResolveAsync(ct);
        var query = _db.Customers.AsNoTracking().InScope(scope);

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var search = filter.Search.Trim();
            query = query.Where(c => c.Name.Contains(search) || c.Email.Contains(search) || c.Phone.Contains(search));
        }

        if (filter.Status.HasValue)
        {
            query = query.Where(c => c.Status == filter.Status.Value);
        }

        if (!string.IsNullOrEmpty(filter.OwnerId))
        {
            if (scope.All || scope.OwnerIds.Contains(filter.OwnerId))
            {
                query = query.Where(c => c.OwnerId == filter.OwnerId);
            }
        }

        if (filter.Range != null)
        {
            var fromUtc = filter.Range.FromUtc(_clock);
            if (fromUtc.HasValue)
            {
                query = query.Where(c => c.CreatedDate >= fromUtc.Value);
            }
            var toExclusiveUtc = filter.Range.ToExclusiveUtc(_clock);
            if (toExclusiveUtc.HasValue)
            {
                query = query.Where(c => c.CreatedDate < toExclusiveUtc.Value);
            }
        }

        var total = await query.CountAsync(ct);

        var paged = await query
            .OrderByDescending(c => c.CreatedDate)
            .Skip(pageQuery.Skip)
            .Take(pageQuery.Take)
            .Select(c => new
            {
                c.Id,
                c.Code,
                c.Name,
                c.CompanyName,
                c.Email,
                c.Phone,
                c.Status,
                c.OwnerId,
                c.CreatedDate
            })
            .ToListAsync(ct);

        var ownerIds = paged.Select(c => c.OwnerId).Distinct().ToList();
        var userNames = await _db.Users.AsNoTracking()
            .Where(u => ownerIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.FullName, ct);

        var items = paged.Select(c => new CustomerReportItem(
            c.Id,
            c.Code,
            c.Name,
            c.CompanyName,
            c.Email,
            c.Phone,
            c.Status,
            userNames.TryGetValue(c.OwnerId, out var name) ? name : c.OwnerId,
            c.CreatedDate)).ToList();

        return new PagedResult<CustomerReportItem>(items, pageQuery.Page, pageQuery.PageSize, total);
    }

    public async Task<PagedResult<FollowUpReportItem>> RunFollowUpReportAsync(FollowUpReportFilter filter, PageQuery pageQuery, CancellationToken ct = default)
    {
        var scope = await _ownerScope.ResolveAsync(ct);
        var query = _db.FollowUps.AsNoTracking().InScope(scope);

        if (filter.Status.HasValue)
        {
            query = query.Where(f => f.Status == filter.Status.Value);
        }

        if (filter.Overdue.HasValue && filter.Overdue.Value)
        {
            var now = _clock.UtcNow;
            query = query.Where(f => f.Status == FollowUpStatus.Planned && f.FollowUpDate < now);
        }

        if (!string.IsNullOrEmpty(filter.AssignedToId))
        {
            if (scope.All || scope.OwnerIds.Contains(filter.AssignedToId))
            {
                query = query.Where(f => f.OwnerId == filter.AssignedToId);
            }
        }

        if (filter.Range != null)
        {
            var fromUtc = filter.Range.FromUtc(_clock);
            if (fromUtc.HasValue)
            {
                query = query.Where(f => f.FollowUpDate >= fromUtc.Value);
            }
            var toExclusiveUtc = filter.Range.ToExclusiveUtc(_clock);
            if (toExclusiveUtc.HasValue)
            {
                query = query.Where(f => f.FollowUpDate < toExclusiveUtc.Value);
            }
        }

        var total = await query.CountAsync(ct);

        var paged = await query
            .OrderByDescending(f => f.FollowUpDate)
            .Skip(pageQuery.Skip)
            .Take(pageQuery.Take)
            .Select(f => new
            {
                f.Id,
                RelatedType = f.CustomerId.HasValue ? RelatedType.Customer : (f.LeadId.HasValue ? RelatedType.Lead : RelatedType.Opportunity),
                CustomerName = f.Customer != null ? f.Customer.Name : null,
                LeadName = f.Lead != null ? f.Lead.Name : null,
                OpportunityName = f.Opportunity != null ? f.Opportunity.Name : null,
                f.Subject,
                f.Type,
                f.Status,
                f.FollowUpDate,
                f.OwnerId
            })
            .ToListAsync(ct);

        var ownerIds = paged.Select(f => f.OwnerId).Distinct().ToList();
        var userNames = await _db.Users.AsNoTracking()
            .Where(u => ownerIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.FullName, ct);

        var nowUtc = _clock.UtcNow;

        var items = paged.Select(f =>
        {
            var relatedName = f.CustomerName ?? f.LeadName ?? f.OpportunityName ?? "Unknown";
            var assignedTo = userNames.TryGetValue(f.OwnerId, out var name) ? name : f.OwnerId;
            var isOverdue = f.Status == FollowUpStatus.Planned && f.FollowUpDate < nowUtc;
            return new FollowUpReportItem(
                f.Id,
                f.RelatedType,
                relatedName,
                f.Subject,
                f.Type,
                f.Status,
                f.FollowUpDate,
                assignedTo,
                isOverdue);
        }).ToList();

        return new PagedResult<FollowUpReportItem>(items, pageQuery.Page, pageQuery.PageSize, total);
    }
}

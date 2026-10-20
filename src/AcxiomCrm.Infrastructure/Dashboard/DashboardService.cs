using AcxiomCrm.Application.Abstractions;
using AcxiomCrm.Application.Common;
using AcxiomCrm.Application.Dashboard;
using AcxiomCrm.Application.FollowUps;
using AcxiomCrm.Application.Reporting;
using AcxiomCrm.Domain.Enums;
using AcxiomCrm.Domain.Security;
using AcxiomCrm.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AcxiomCrm.Infrastructure.Dashboard;

public sealed class DashboardService : IDashboardService
{
    private readonly CrmDbContext _db;
    private readonly IOwnerScope _ownerScope;
    private readonly ICurrentUser _currentUser;
    private readonly IClock _clock;

    public DashboardService(
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

    public async Task<Result<DashboardData>> GetAsync(DatePreset preset, DateOnly? from, DateOnly? to, CancellationToken ct = default)
    {
        var rangeResult = DateRangeResolver.Resolve(preset, from, to, _clock);
        if (!rangeResult.Succeeded)
        {
            return Result<DashboardData>.Fail(rangeResult.Kind, rangeResult.Errors);
        }

        var range = rangeResult.Value!;
        var fromUtc = range.FromUtc(_clock);
        var toExclusiveUtc = range.ToExclusiveUtc(_clock);

        var scope = await _ownerScope.ResolveAsync(ct);

        // 1. Customers
        var custQuery = _db.Customers.AsNoTracking().InScope(scope);
        var totalCustomers = await custQuery.CountAsync(ct);
        int? customerDelta = null;
        if (fromUtc.HasValue && toExclusiveUtc.HasValue)
        {
            customerDelta = await custQuery.CountAsync(c => c.CreatedDate >= fromUtc.Value && c.CreatedDate < toExclusiveUtc.Value, ct);
        }

        // 2. Leads
        var leadQuery = _db.Leads.AsNoTracking().InScope(scope);
        var totalLeads = await leadQuery.CountAsync(ct);
        int? leadDelta = null;
        if (fromUtc.HasValue && toExclusiveUtc.HasValue)
        {
            leadDelta = await leadQuery.CountAsync(l => l.CreatedDate >= fromUtc.Value && l.CreatedDate < toExclusiveUtc.Value, ct);
        }

        var openLeads = await leadQuery.CountAsync(l =>
            l.Status == LeadStatus.New ||
            l.Status == LeadStatus.Contacted ||
            l.Status == LeadStatus.Qualified, ct);

        // Lead Status Chart (all 6 statuses)
        var leadStatusCounts = await leadQuery
            .GroupBy(l => l.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToDictionaryAsync(g => g.Status, g => g.Count, ct);

        var leadStatuses = new[]
        {
            LeadStatus.New,
            LeadStatus.Contacted,
            LeadStatus.Qualified,
            LeadStatus.Unqualified,
            LeadStatus.Converted,
            LeadStatus.Lost
        };

        var leadStatusChart = leadStatuses.Select(s => new LabelValue(
            s.ToString(),
            leadStatusCounts.TryGetValue(s, out var count) ? count : 0)).ToList();

        // 3. Opportunities
        var oppQuery = _db.Opportunities.AsNoTracking().InScope(scope);
        var totalOpportunities = await oppQuery.CountAsync(ct);
        var openOpportunities = await oppQuery.CountAsync(o => o.Status == OpportunityStatus.Open, ct);
        var wonOpportunities = await oppQuery.CountAsync(o => o.Status == OpportunityStatus.Won, ct);
        var lostOpportunities = await oppQuery.CountAsync(o => o.Status == OpportunityStatus.Lost, ct);

        var openOpps = await oppQuery
            .Where(o => o.Status == OpportunityStatus.Open)
            .Select(o => new { o.Amount, o.Probability, o.Stage, o.OwnerId })
            .ToListAsync(ct);

        var pipelineValue = openOpps.Sum(o => o.Amount);
        var weightedPipeline = openOpps.Sum(o => Math.Round(o.Amount * o.Probability / 100m, 2));

        // Pipeline by stage (Open stages)
        var openStages = new[]
        {
            OpportunityStage.Qualification,
            OpportunityStage.Proposal,
            OpportunityStage.Negotiation
        };

        var pipelineChart = openStages.Select(stage =>
        {
            var inStage = openOpps.Where(o => o.Stage == stage).ToList();
            var count = inStage.Count;
            var amt = inStage.Sum(o => o.Amount);
            var wgt = inStage.Sum(o => Math.Round(o.Amount * o.Probability / 100m, 2));
            return new StageSeries(stage.ToString(), count, amt, wgt);
        }).ToList();

        // Monthly sales chart (last 6 months up to current month)
        var today = _clock.Today;
        var monthsList = new List<DateOnly>();
        for (int i = 5; i >= 0; i--)
        {
            var d = today.AddMonths(-i);
            monthsList.Add(new DateOnly(d.Year, d.Month, 1));
        }

        var startSixMonthsAgo = monthsList[0];
        var startSixMonthsAgoUtc = _clock.ToUtcFromBusinessLocal(startSixMonthsAgo.ToDateTime(TimeOnly.MinValue));

        var wonOpps = await oppQuery
            .Where(o => o.Status == OpportunityStatus.Won && o.ClosedDate.HasValue && o.ClosedDate.Value >= startSixMonthsAgo)
            .Select(o => new { o.ClosedDate, o.Amount })
            .ToListAsync(ct);

        var monthlySalesChart = monthsList.Select(m =>
        {
            var inMonth = wonOpps.Where(o => o.ClosedDate.HasValue && o.ClosedDate.Value.Year == m.Year && o.ClosedDate.Value.Month == m.Month).ToList();
            return new MonthPoint(m, inMonth.Sum(o => o.Amount), inMonth.Count);
        }).ToList();

        // Cards list (8 KPI cards)
        var cards = new List<KpiCard>
        {
            new("totalCustomers", "Total Customers", totalCustomers, false, customerDelta, "Total accounts in your scope"),
            new("totalLeads", "Total Leads", totalLeads, false, leadDelta, "All generated leads"),
            new("openLeads", "Open Leads", openLeads, false, null, "New, contacted, or qualified"),
            new("totalOpportunities", "Total Deals", totalOpportunities, false, null, "All opportunity records"),
            new("openOpportunities", "Open Pipeline Deals", openOpportunities, false, null, "Active open opportunities"),
            new("wonOpportunities", "Won Deals", wonOpportunities, false, null, "Successfully closed deals"),
            new("lostOpportunities", "Lost Deals", lostOpportunities, false, null, "Closed lost opportunities"),
            new("pipelineValue", "Pipeline Value", pipelineValue, true, null, "Total value of open opportunities")
        };

        // 4. Role Panels
        AdminPanel? adminPanel = null;
        if (string.Equals(_currentUser.Role, Roles.Admin, StringComparison.OrdinalIgnoreCase))
        {
            var activeUsers = await _db.Users.CountAsync(u => u.IsActive, ct);
            var inactiveUsers = await _db.Users.CountAsync(u => !u.IsActive, ct);
            var nowUtc = _clock.UtcNow;
            var lockedOutUsers = await _db.Users.CountAsync(u => u.LockoutEnd != null && u.LockoutEnd > nowUtc, ct);

            var yesterdayUtc = nowUtc.AddHours(-24);
            var failedLogins = await _db.AuditLogs.CountAsync(a => a.Action == "FailedLogin" && a.CreatedDate >= yesterdayUtc, ct);

            var sevenDaysAgoUtc = nowUtc.AddDays(-7);
            var securityEvents = await _db.AuditLogs.CountAsync(a => a.Module == "Security" && a.CreatedDate >= sevenDaysAgoUtc, ct);

            adminPanel = new AdminPanel(activeUsers, inactiveUsers, lockedOutUsers, failedLogins, securityEvents);
        }

        IReadOnlyList<OwnerPipeline>? teamPipeline = null;
        if (string.Equals(_currentUser.Role, Roles.Admin, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(_currentUser.Role, Roles.Manager, StringComparison.OrdinalIgnoreCase))
        {
            var openOwnerIds = openOpps.Select(o => o.OwnerId).Distinct().ToList();
            var userNames = await _db.Users.AsNoTracking()
                .Where(u => openOwnerIds.Contains(u.Id))
                .ToDictionaryAsync(u => u.Id, u => u.FullName, ct);

            teamPipeline = openOpps.GroupBy(o => o.OwnerId)
                .Select(g =>
                {
                    var name = userNames.TryGetValue(g.Key, out var n) ? n : g.Key;
                    return new OwnerPipeline(
                        g.Key,
                        name,
                        g.Count(),
                        g.Sum(o => o.Amount),
                        g.Sum(o => Math.Round(o.Amount * o.Probability / 100m, 2)));
                })
                .OrderByDescending(p => p.Amount)
                .ToList();
        }

        // FollowUpPanel (for all roles)
        var followUpQuery = _db.FollowUps.AsNoTracking().InScope(scope);
        var nowFollowUpUtc = _clock.UtcNow;
        var todayStartUtc = _clock.ToUtcFromBusinessLocal(today.ToDateTime(TimeOnly.MinValue));
        var tomorrowStartUtc = _clock.ToUtcFromBusinessLocal(today.AddDays(1).ToDateTime(TimeOnly.MinValue));
        var in7DaysUtc = _clock.ToUtcFromBusinessLocal(today.AddDays(7).ToDateTime(TimeOnly.MinValue));

        var overdueCount = await followUpQuery.CountAsync(f =>
            f.Status == FollowUpStatus.Planned && f.FollowUpDate < nowFollowUpUtc, ct);

        var dueTodayCount = await followUpQuery.CountAsync(f =>
            f.Status == FollowUpStatus.Planned && f.FollowUpDate >= todayStartUtc && f.FollowUpDate < tomorrowStartUtc, ct);

        var next7DaysCount = await followUpQuery.CountAsync(f =>
            f.Status == FollowUpStatus.Planned && f.FollowUpDate >= todayStartUtc && f.FollowUpDate < in7DaysUtc, ct);

        var next5Items = await followUpQuery
            .Where(f => f.Status == FollowUpStatus.Planned && f.FollowUpDate >= todayStartUtc)
            .OrderBy(f => f.FollowUpDate)
            .Take(5)
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
                f.OwnerId,
                f.RescheduleCount
            })
            .ToListAsync(ct);

        var next5OwnerIds = next5Items.Select(f => f.OwnerId).Distinct().ToList();
        var next5UserNames = await _db.Users.AsNoTracking()
            .Where(u => next5OwnerIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.FullName, ct);

        var next5List = next5Items.Select(f => new FollowUpListItem(
            f.Id,
            f.RelatedType,
            0,
            f.CustomerName ?? f.LeadName ?? f.OpportunityName ?? "Unknown",
            false,
            f.FollowUpDate,
            f.Subject,
            f.Type,
            f.Status,
            f.FollowUpDate < nowFollowUpUtc,
            f.FollowUpDate >= todayStartUtc && f.FollowUpDate < tomorrowStartUtc,
            f.OwnerId,
            next5UserNames.TryGetValue(f.OwnerId, out var uname) ? uname : f.OwnerId,
            f.RescheduleCount)).ToList();

        var followUpPanel = new FollowUpPanel(overdueCount, dueTodayCount, next7DaysCount, next5List);
        var panels = new RolePanels(adminPanel, teamPipeline, followUpPanel);

        return Result<DashboardData>.Ok(new DashboardData(
            range,
            cards,
            leadStatusChart,
            pipelineChart,
            monthlySalesChart,
            weightedPipeline,
            panels));
    }
}

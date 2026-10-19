using AcxiomCrm.Application.Abstractions;
using AcxiomCrm.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace AcxiomCrm.Infrastructure.Users;

public interface IWorkReassigner
{
    Task<int> ReassignOpenWorkAsync(string fromUserId, string toUserId, CancellationToken ct = default);
    Task<(int openLeads, int openOpportunities, int plannedFollowUps)> GetOpenWorkCountsAsync(string userId, CancellationToken ct = default);
}

public sealed class WorkReassigner : IWorkReassigner
{
    private readonly ICrmDbContext _db;

    public WorkReassigner(ICrmDbContext db)
    {
        _db = db;
    }

    public async Task<int> ReassignOpenWorkAsync(string fromUserId, string toUserId, CancellationToken ct = default)
    {
        var openLeads = await _db.Leads
            .Where(l => l.OwnerId == fromUserId && l.Status != LeadStatus.Converted && l.Status != LeadStatus.Lost)
            .ToListAsync(ct);

        foreach (var lead in openLeads)
        {
            lead.AssignTo(toUserId);
        }

        var openOpps = await _db.Opportunities
            .Where(o => o.OwnerId == fromUserId && o.Stage != OpportunityStage.Won && o.Stage != OpportunityStage.Lost)
            .ToListAsync(ct);

        foreach (var opp in openOpps)
        {
            opp.AssignTo(toUserId);
        }

        var plannedFollowUps = await _db.FollowUps
            .Where(f => f.OwnerId == fromUserId && f.Status == FollowUpStatus.Planned)
            .ToListAsync(ct);

        foreach (var fu in plannedFollowUps)
        {
            fu.AssignTo(toUserId);
        }

        var activities = await _db.Activities
            .Where(a => a.OwnerId == fromUserId)
            .ToListAsync(ct);

        foreach (var act in activities)
        {
            act.AssignTo(toUserId);
        }

        await _db.SaveChangesAsync(ct);

        return openLeads.Count + openOpps.Count + plannedFollowUps.Count + activities.Count;
    }

    public async Task<(int openLeads, int openOpportunities, int plannedFollowUps)> GetOpenWorkCountsAsync(string userId, CancellationToken ct = default)
    {
        var leads = await _db.Leads
            .CountAsync(l => l.OwnerId == userId && l.Status != LeadStatus.Converted && l.Status != LeadStatus.Lost, ct);

        var opps = await _db.Opportunities
            .CountAsync(o => o.OwnerId == userId && o.Stage != OpportunityStage.Won && o.Stage != OpportunityStage.Lost, ct);

        var followUps = await _db.FollowUps
            .CountAsync(f => f.OwnerId == userId && f.Status == FollowUpStatus.Planned, ct);

        return (leads, opps, followUps);
    }
}

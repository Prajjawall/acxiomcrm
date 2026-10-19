using AcxiomCrm.Application.Abstractions;
using AcxiomCrm.Application.Common;
using AcxiomCrm.Application.FollowUps;
using AcxiomCrm.Application.Users;
using AcxiomCrm.Domain.Audit;
using AcxiomCrm.Domain.Entities;
using AcxiomCrm.Domain.Enums;
using AcxiomCrm.Domain.Rules;
using AcxiomCrm.Domain.Security;
using AcxiomCrm.Domain.Validation;
using AcxiomCrm.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AcxiomCrm.Infrastructure.FollowUps;

public sealed class FollowUpService : IFollowUpService
{
    private readonly CrmDbContext _db;
    private readonly ICurrentUser _currentUser;
    private readonly IOwnerScope _ownerScope;
    private readonly IClock _clock;
    private readonly IUserDirectory _userDirectory;
    private readonly IAuditActionHint? _actionHint;

    public FollowUpService(
        CrmDbContext db,
        ICurrentUser currentUser,
        IOwnerScope ownerScope,
        IClock clock,
        IUserDirectory userDirectory,
        IAuditActionHint? actionHint = null)
    {
        _db = db;
        _currentUser = currentUser;
        _ownerScope = ownerScope;
        _clock = clock;
        _userDirectory = userDirectory;
        _actionHint = actionHint;
    }

    public async Task<PagedResult<FollowUpListItem>> SearchAsync(FollowUpFilter f, PageQuery p, CancellationToken ct = default)
    {
        if (!_currentUser.Can(Permissions.FollowUpsView))
        {
            return new PagedResult<FollowUpListItem>([], p.Page, p.PageSize, 0);
        }

        var scope = await _ownerScope.ResolveAsync(ct);
        var query = _db.FollowUps.AsNoTracking().InScope(scope)
            .Include(x => x.Customer)
            .Include(x => x.Lead)
            .Include(x => x.Opportunity)
            .AsQueryable();

        if (f.Status.HasValue)
        {
            query = query.Where(x => x.Status == f.Status.Value);
        }

        if (!string.IsNullOrWhiteSpace(f.AssignedToId))
        {
            query = query.Where(x => x.OwnerId == f.AssignedToId);
        }

        if (f.Related is not null)
        {
            query = f.Related.Type switch
            {
                RelatedType.Customer => query.Where(x => x.CustomerId == f.Related.Id),
                RelatedType.Lead => query.Where(x => x.LeadId == f.Related.Id),
                RelatedType.Opportunity => query.Where(x => x.OpportunityId == f.Related.Id),
                _ => query
            };
        }

        if (f.From.HasValue)
        {
            var fromUtc = _clock.ToUtcFromBusinessLocal(f.From.Value.ToDateTime(TimeOnly.MinValue));
            query = query.Where(x => x.FollowUpDate >= fromUtc);
        }

        if (f.To.HasValue)
        {
            var toUtc = _clock.ToUtcFromBusinessLocal(f.To.Value.AddDays(1).ToDateTime(TimeOnly.MinValue));
            query = query.Where(x => x.FollowUpDate < toUtc);
        }

        var nowUtc = _clock.UtcNow;
        if (f.OverdueOnly)
        {
            query = query.Where(x => x.Status == FollowUpStatus.Planned && x.FollowUpDate < nowUtc);
        }

        if (f.DueTodayOnly)
        {
            var today = _clock.Today;
            var startTodayUtc = _clock.ToUtcFromBusinessLocal(today.ToDateTime(TimeOnly.MinValue));
            var endTodayUtc = _clock.ToUtcFromBusinessLocal(today.AddDays(1).ToDateTime(TimeOnly.MinValue));
            query = query.Where(x => x.Status == FollowUpStatus.Planned && x.FollowUpDate >= startTodayUtc && x.FollowUpDate < endTodayUtc);
        }

        var total = await query.CountAsync(ct);

        // Sorting: default overdue first, then date asc
        var items = await query
            .OrderBy(x => x.Status == FollowUpStatus.Planned && x.FollowUpDate < nowUtc ? 0 : 1)
            .ThenBy(x => x.FollowUpDate)
            .Skip((p.Page - 1) * p.PageSize)
            .Take(p.PageSize)
            .ToListAsync(ct);

        var userIds = items.Select(x => x.AssignedToId).Distinct().ToList();
        var userNames = await _db.Users.AsNoTracking()
            .Where(u => userIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.FullName, ct);

        var listItems = items.Select(x =>
        {
            var isOverdue = FollowUpRules.IsOverdue(x.Status, x.FollowUpDate, nowUtc);
            var isDueToday = FollowUpRules.IsDueToday(x.Status, _clock.BusinessDateOf(x.FollowUpDate), _clock.Today);
            var (relId, relName, relDeactivated) = GetRelatedInfo(x);
            var assignedName = userNames.TryGetValue(x.AssignedToId, out var uname) ? uname : x.AssignedToId;

            return new FollowUpListItem(
                x.Id,
                x.RelatedType,
                relId,
                relName,
                relDeactivated,
                x.FollowUpDate,
                x.Subject,
                x.Type,
                x.Status,
                isOverdue,
                isDueToday,
                x.AssignedToId,
                assignedName,
                x.RescheduleCount);
        }).ToList();

        return new PagedResult<FollowUpListItem>(listItems, p.Page, p.PageSize, total);
    }

    public async Task<Result<FollowUpDetail>> GetAsync(int id, CancellationToken ct = default)
    {
        if (!_currentUser.Can(Permissions.FollowUpsView))
        {
            return Result<FollowUpDetail>.Fail(ErrorKind.Forbidden);
        }

        var scope = await _ownerScope.ResolveAsync(ct);
        var followUp = await _db.FollowUps.AsNoTracking().InScope(scope)
            .Include(x => x.Customer)
            .Include(x => x.Lead)
            .Include(x => x.Opportunity)
            .FirstOrDefaultAsync(x => x.Id == id, ct);

        if (followUp is null)
        {
            return Result<FollowUpDetail>.Fail(ErrorKind.NotFound);
        }

        return Result<FollowUpDetail>.Ok(await MapDetailAsync(followUp, ct));
    }

    public async Task<Result<FollowUpDetail>> CreateAsync(CreateFollowUpCommand c, CancellationToken ct = default)
    {
        if (!_currentUser.Can(Permissions.FollowUpsCreate))
        {
            return Result<FollowUpDetail>.Fail(ErrorKind.Forbidden);
        }

        if (c.IsHistoricalEntry && !_currentUser.Can(Permissions.FollowUpsHistorical))
        {
            return Result<FollowUpDetail>.Fail(ErrorKind.Forbidden);
        }

        var businessDate = _clock.BusinessDateOf(c.FollowUpDateUtc);
        var errors = FollowUpRules.Validate(
            c.Subject,
            c.Remarks,
            businessDate,
            _clock.Today,
            c.IsHistoricalEntry,
            c.Related is null ? 0 : 1);

        if (errors.Count > 0)
        {
            return Result<FollowUpDetail>.Fail(ErrorKind.Validation, errors);
        }

        // Verify related record in scope
        var scope = await _ownerScope.ResolveAsync(ct);
        int? customerId = null;
        int? leadId = null;
        int? opportunityId = null;

        switch (c.Related!.Type)
        {
            case RelatedType.Customer:
                var cust = await _db.Customers.AsNoTracking().InScope(scope).FirstOrDefaultAsync(x => x.Id == c.Related.Id, ct);
                if (cust is null)
                {
                    return Result<FollowUpDetail>.Fail(ErrorKind.Validation, [new FieldError("Related", "Choose an active customer.")]);
                }
                customerId = cust.Id;
                break;

            case RelatedType.Lead:
                var lead = await _db.Leads.AsNoTracking().InScope(scope).FirstOrDefaultAsync(x => x.Id == c.Related.Id, ct);
                if (lead is null)
                {
                    return Result<FollowUpDetail>.Fail(ErrorKind.Validation, [new FieldError("Related", "Choose an active lead.")]);
                }
                leadId = lead.Id;
                break;

            case RelatedType.Opportunity:
                var opp = await _db.Opportunities.AsNoTracking().InScope(scope).FirstOrDefaultAsync(x => x.Id == c.Related.Id, ct);
                if (opp is null)
                {
                    return Result<FollowUpDetail>.Fail(ErrorKind.Validation, [new FieldError("Related", "Choose an active opportunity.")]);
                }
                opportunityId = opp.Id;
                break;
        }

        // Assignee resolution
        string assignedToId;
        if (_currentUser.Role == Roles.SalesExecutive)
        {
            assignedToId = _currentUser.UserId ?? string.Empty;
        }
        else
        {
            assignedToId = string.IsNullOrWhiteSpace(c.AssignedToId) ? (_currentUser.UserId ?? string.Empty) : c.AssignedToId;
            var assignable = await _userDirectory.ListAssignableOwnersAsync(ct);
            if (!assignable.Any(u => u.Id == assignedToId))
            {
                return Result<FollowUpDetail>.Fail(ErrorKind.Validation, [new FieldError("AssignedToId", "Choose an active assignable user.")]);
            }
        }

        var followUp = FollowUp.Create(
            customerId,
            leadId,
            opportunityId,
            c.FollowUpDateUtc,
            c.Subject.Trim(),
            c.Type,
            c.Remarks?.Trim(),
            assignedToId,
            c.IsHistoricalEntry);

        _db.FollowUps.Add(followUp);
        await _db.SaveChangesAsync(ct);

        return await GetAsync(followUp.Id, ct);
    }

    public async Task<Result<FollowUpDetail>> UpdateAsync(int id, UpdateFollowUpCommand c, CancellationToken ct = default)
    {
        if (!_currentUser.Can(Permissions.FollowUpsEdit))
        {
            return Result<FollowUpDetail>.Fail(ErrorKind.Forbidden);
        }

        var scope = await _ownerScope.ResolveAsync(ct);
        var followUp = await _db.FollowUps.InScope(scope).FirstOrDefaultAsync(x => x.Id == id, ct);
        if (followUp is null)
        {
            return Result<FollowUpDetail>.Fail(ErrorKind.NotFound);
        }

        if (followUp.Status != FollowUpStatus.Planned)
        {
            return Result<FollowUpDetail>.Fail(ErrorKind.Conflict);
        }

        if (c.RowVersion.Length > 0 && !c.RowVersion.SequenceEqual(followUp.RowVersion))
        {
            return ConcurrencyHelper.Conflict<FollowUpDetail>("Follow-up");
        }

        var businessDate = _clock.BusinessDateOf(c.FollowUpDateUtc);
        var errors = FollowUpRules.Validate(c.Subject, c.Remarks, businessDate, _clock.Today, false, 1);
        if (errors.Count > 0)
        {
            return Result<FollowUpDetail>.Fail(ErrorKind.Validation, errors);
        }

        string assignedToId = followUp.AssignedToId;
        if (_currentUser.Role != Roles.SalesExecutive && !string.IsNullOrWhiteSpace(c.AssignedToId))
        {
            var assignable = await _userDirectory.ListAssignableOwnersAsync(ct);
            if (!assignable.Any(u => u.Id == c.AssignedToId))
            {
                return Result<FollowUpDetail>.Fail(ErrorKind.Validation, [new FieldError("AssignedToId", "Choose an active assignable user.")]);
            }
            assignedToId = c.AssignedToId;
        }

        // Apply updates
        if (c.FollowUpDateUtc != followUp.FollowUpDate)
        {
            followUp.Reschedule(c.FollowUpDateUtc);
        }

        followUp.Update(c.Subject.Trim(), c.Type, c.Remarks?.Trim(), assignedToId);

        if (c.RowVersion.Length > 0)
        {
            _db.Entry(followUp).Property(x => x.RowVersion).OriginalValue = c.RowVersion;
        }

        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            return ConcurrencyHelper.Conflict<FollowUpDetail>("Follow-up");
        }

        return await GetAsync(followUp.Id, ct);
    }

    public async Task<Result<FollowUpDetail>> CompleteAsync(int id, CompleteFollowUpCommand c, CancellationToken ct = default)
    {
        if (!_currentUser.Can(Permissions.FollowUpsEdit))
        {
            return Result<FollowUpDetail>.Fail(ErrorKind.Forbidden);
        }

        var scope = await _ownerScope.ResolveAsync(ct);
        var followUp = await _db.FollowUps.InScope(scope).FirstOrDefaultAsync(x => x.Id == id, ct);
        if (followUp is null)
        {
            return Result<FollowUpDetail>.Fail(ErrorKind.NotFound);
        }

        if (followUp.Status != FollowUpStatus.Planned)
        {
            return Result<FollowUpDetail>.Fail(ErrorKind.Conflict);
        }

        if (c.RowVersion.Length > 0 && !c.RowVersion.SequenceEqual(followUp.RowVersion))
        {
            return ConcurrencyHelper.Conflict<FollowUpDetail>("Follow-up");
        }

        followUp.Complete(c.Remarks?.Trim(), _clock.UtcNow);

        _actionHint?.Set(AuditActions.FollowUpCompleted, "Completed");

        if (c.LogActivity)
        {
            var actType = followUp.Type switch
            {
                FollowUpType.Call => ActivityType.Call,
                FollowUpType.Meeting => ActivityType.Meeting,
                FollowUpType.Email => ActivityType.Email,
                _ => ActivityType.Task
            };

            var activity = Activity.Create(
                actType,
                followUp.Subject,
                c.Remarks?.Trim() ?? followUp.Remarks,
                _clock.UtcNow,
                followUp.CustomerId,
                followUp.LeadId,
                followUp.OpportunityId,
                followUp.AssignedToId,
                ActivityStatus.Completed);

            _db.Activities.Add(activity);
        }

        if (c.RowVersion.Length > 0)
        {
            _db.Entry(followUp).Property(x => x.RowVersion).OriginalValue = c.RowVersion;
        }

        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            return ConcurrencyHelper.Conflict<FollowUpDetail>("Follow-up");
        }

        return await GetAsync(followUp.Id, ct);
    }

    public async Task<Result<FollowUpDetail>> RescheduleAsync(int id, DateTime newDateUtc, byte[] rowVersion, CancellationToken ct = default)
    {
        if (!_currentUser.Can(Permissions.FollowUpsEdit))
        {
            return Result<FollowUpDetail>.Fail(ErrorKind.Forbidden);
        }

        var scope = await _ownerScope.ResolveAsync(ct);
        var followUp = await _db.FollowUps.InScope(scope).FirstOrDefaultAsync(x => x.Id == id, ct);
        if (followUp is null)
        {
            return Result<FollowUpDetail>.Fail(ErrorKind.NotFound);
        }

        if (followUp.Status != FollowUpStatus.Planned)
        {
            return Result<FollowUpDetail>.Fail(ErrorKind.Conflict);
        }

        if (rowVersion.Length > 0 && !rowVersion.SequenceEqual(followUp.RowVersion))
        {
            return ConcurrencyHelper.Conflict<FollowUpDetail>("Follow-up");
        }

        var businessDate = _clock.BusinessDateOf(newDateUtc);
        if (businessDate < _clock.Today)
        {
            return Result<FollowUpDetail>.Fail(ErrorKind.Validation, [new FieldError("FollowUpDate", Messages.FollowUpDateBeforeToday)]);
        }

        followUp.Reschedule(newDateUtc);

        _actionHint?.Set(AuditActions.FollowUpRescheduled, "Rescheduled");

        if (rowVersion.Length > 0)
        {
            _db.Entry(followUp).Property(x => x.RowVersion).OriginalValue = rowVersion;
        }

        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            return ConcurrencyHelper.Conflict<FollowUpDetail>("Follow-up");
        }

        return await GetAsync(followUp.Id, ct);
    }

    public async Task<Result<FollowUpDetail>> MarkMissedAsync(int id, byte[] rowVersion, CancellationToken ct = default)
    {
        if (!_currentUser.Can(Permissions.FollowUpsEdit))
        {
            return Result<FollowUpDetail>.Fail(ErrorKind.Forbidden);
        }

        var scope = await _ownerScope.ResolveAsync(ct);
        var followUp = await _db.FollowUps.InScope(scope).FirstOrDefaultAsync(x => x.Id == id, ct);
        if (followUp is null)
        {
            return Result<FollowUpDetail>.Fail(ErrorKind.NotFound);
        }

        if (followUp.Status != FollowUpStatus.Planned)
        {
            return Result<FollowUpDetail>.Fail(ErrorKind.Conflict);
        }

        if (rowVersion.Length > 0 && !rowVersion.SequenceEqual(followUp.RowVersion))
        {
            return ConcurrencyHelper.Conflict<FollowUpDetail>("Follow-up");
        }

        followUp.MarkMissed();

        _actionHint?.Set(AuditActions.FollowUpMissed, "Missed");

        if (rowVersion.Length > 0)
        {
            _db.Entry(followUp).Property(x => x.RowVersion).OriginalValue = rowVersion;
        }

        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            return ConcurrencyHelper.Conflict<FollowUpDetail>("Follow-up");
        }

        return await GetAsync(followUp.Id, ct);
    }

    public async Task<Result<FollowUpDetail>> CancelAsync(int id, byte[] rowVersion, CancellationToken ct = default)
    {
        if (!_currentUser.Can(Permissions.FollowUpsEdit))
        {
            return Result<FollowUpDetail>.Fail(ErrorKind.Forbidden);
        }

        var scope = await _ownerScope.ResolveAsync(ct);
        var followUp = await _db.FollowUps.InScope(scope).FirstOrDefaultAsync(x => x.Id == id, ct);
        if (followUp is null)
        {
            return Result<FollowUpDetail>.Fail(ErrorKind.NotFound);
        }

        if (followUp.Status != FollowUpStatus.Planned)
        {
            return Result<FollowUpDetail>.Fail(ErrorKind.Conflict);
        }

        if (rowVersion.Length > 0 && !rowVersion.SequenceEqual(followUp.RowVersion))
        {
            return ConcurrencyHelper.Conflict<FollowUpDetail>("Follow-up");
        }

        followUp.Cancel();

        _actionHint?.Set(AuditActions.StatusChanged, "Cancelled");

        if (rowVersion.Length > 0)
        {
            _db.Entry(followUp).Property(x => x.RowVersion).OriginalValue = rowVersion;
        }

        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            return ConcurrencyHelper.Conflict<FollowUpDetail>("Follow-up");
        }

        return await GetAsync(followUp.Id, ct);
    }

    public async Task<Result> DeleteAsync(int id, CancellationToken ct = default)
    {
        if (!_currentUser.Can(Permissions.FollowUpsDelete))
        {
            return Result.Fail(ErrorKind.Forbidden);
        }

        var scope = await _ownerScope.ResolveAsync(ct);
        var followUp = await _db.FollowUps.InScope(scope).FirstOrDefaultAsync(x => x.Id == id, ct);
        if (followUp is null)
        {
            return Result.Fail(ErrorKind.NotFound);
        }

        // Executives can only delete their own planned follow-ups
        if (_currentUser.Role == Roles.SalesExecutive && (followUp.AssignedToId != _currentUser.UserId || followUp.Status != FollowUpStatus.Planned))
        {
            return Result.Fail(ErrorKind.Forbidden);
        }

        followUp.SoftDelete(_currentUser.UserId ?? "system", _clock.UtcNow);
        await _db.SaveChangesAsync(ct);
        return Result.Ok();
    }

    public async Task<NotificationCounts> GetNotificationCountsAsync(CancellationToken ct = default)
    {
        var nowUtc = _clock.UtcNow;
        var today = _clock.Today;
        var startTodayUtc = _clock.ToUtcFromBusinessLocal(today.ToDateTime(TimeOnly.MinValue));
        var endTodayUtc = _clock.ToUtcFromBusinessLocal(today.AddDays(1).ToDateTime(TimeOnly.MinValue));
        var currentUserId = _currentUser.UserId ?? string.Empty;

        var overdueCount = await _db.FollowUps.AsNoTracking()
            .CountAsync(f => f.OwnerId == currentUserId && f.Status == FollowUpStatus.Planned && f.FollowUpDate < nowUtc, ct);

        var dueTodayCount = await _db.FollowUps.AsNoTracking()
            .CountAsync(f => f.OwnerId == currentUserId && f.Status == FollowUpStatus.Planned && f.FollowUpDate >= startTodayUtc && f.FollowUpDate < endTodayUtc, ct);

        return new NotificationCounts(overdueCount, dueTodayCount);
    }

    private static (int Id, string Name, bool Deactivated) GetRelatedInfo(FollowUp f)
    {
        if (f.CustomerId.HasValue)
        {
            return (f.CustomerId.Value, f.Customer?.Name ?? $"Customer #{f.CustomerId}", f.Customer?.IsDeleted == true || f.Customer?.Status == CustomerStatus.Inactive);
        }
        if (f.LeadId.HasValue)
        {
            return (f.LeadId.Value, f.Lead?.Name ?? $"Lead #{f.LeadId}", f.Lead?.IsDeleted == true || f.Lead?.Status == LeadStatus.Lost);
        }
        if (f.OpportunityId.HasValue)
        {
            return (f.OpportunityId.Value, f.Opportunity?.Name ?? $"Opportunity #{f.OpportunityId}", f.Opportunity?.IsDeleted == true);
        }
        return (0, "Unknown", false);
    }

    private async Task<FollowUpDetail> MapDetailAsync(FollowUp f, CancellationToken ct)
    {
        var user = await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == f.AssignedToId, ct);
        var assignedName = user?.FullName ?? f.AssignedToId;
        var (relId, relName, _) = GetRelatedInfo(f);
        var isOverdue = FollowUpRules.IsOverdue(f.Status, f.FollowUpDate, _clock.UtcNow);

        return new FollowUpDetail(
            f.Id,
            f.RelatedType,
            relId,
            relName,
            f.FollowUpDate,
            f.Subject,
            f.Type,
            f.Status,
            f.Remarks,
            f.AssignedToId,
            assignedName,
            f.CompletedDate,
            f.RescheduleCount,
            f.IsHistoricalEntry,
            isOverdue,
            f.CreatedDate,
            f.RowVersion);
    }
}

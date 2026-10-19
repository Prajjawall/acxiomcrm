using AcxiomCrm.Application.Abstractions;
using AcxiomCrm.Application.Activities;
using AcxiomCrm.Application.Common;
using AcxiomCrm.Application.Users;
using AcxiomCrm.Domain.Entities;
using AcxiomCrm.Domain.Enums;
using AcxiomCrm.Domain.Rules;
using AcxiomCrm.Domain.Security;
using AcxiomCrm.Domain.Validation;
using AcxiomCrm.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AcxiomCrm.Infrastructure.Activities;

public sealed class ActivityService : IActivityService
{
    private readonly CrmDbContext _db;
    private readonly ICurrentUser _currentUser;
    private readonly IOwnerScope _ownerScope;
    private readonly IClock _clock;
    private readonly IUserDirectory _userDirectory;

    public ActivityService(
        CrmDbContext db,
        ICurrentUser currentUser,
        IOwnerScope ownerScope,
        IClock clock,
        IUserDirectory userDirectory)
    {
        _db = db;
        _currentUser = currentUser;
        _ownerScope = ownerScope;
        _clock = clock;
        _userDirectory = userDirectory;
    }

    public async Task<PagedResult<ActivityListItem>> SearchAsync(ActivityFilter f, PageQuery p, CancellationToken ct = default)
    {
        if (!_currentUser.Can(Permissions.ActivitiesView))
        {
            return new PagedResult<ActivityListItem>([], p.Page, p.PageSize, 0);
        }

        var scope = await _ownerScope.ResolveAsync(ct);
        var query = _db.Activities.AsNoTracking().InScope(scope).AsQueryable();

        if (f.Type.HasValue)
        {
            query = query.Where(x => x.Type == f.Type.Value);
        }

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
            query = query.Where(x => x.ActivityDate >= fromUtc);
        }

        if (f.To.HasValue)
        {
            var toUtc = _clock.ToUtcFromBusinessLocal(f.To.Value.AddDays(1).ToDateTime(TimeOnly.MinValue));
            query = query.Where(x => x.ActivityDate < toUtc);
        }

        var total = await query.CountAsync(ct);

        var items = await query
            .OrderByDescending(x => x.ActivityDate)
            .Skip((p.Page - 1) * p.PageSize)
            .Take(p.PageSize)
            .ToListAsync(ct);

        var userIds = items.Select(x => x.AssignedToId).Distinct().ToList();
        var userNames = await _db.Users.AsNoTracking()
            .Where(u => userIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.FullName, ct);

        var listItems = items.Select(x =>
        {
            var related = new List<RelatedRef>();
            if (x.CustomerId.HasValue)
            {
                related.Add(new RelatedRef(RelatedType.Customer, x.CustomerId.Value));
            }
            if (x.LeadId.HasValue)
            {
                related.Add(new RelatedRef(RelatedType.Lead, x.LeadId.Value));
            }
            if (x.OpportunityId.HasValue)
            {
                related.Add(new RelatedRef(RelatedType.Opportunity, x.OpportunityId.Value));
            }

            var assignedName = userNames.TryGetValue(x.AssignedToId, out var uname) ? uname : x.AssignedToId;

            return new ActivityListItem(
                x.Id,
                x.Type,
                x.Subject,
                x.Description,
                x.ActivityDate,
                related,
                x.AssignedToId,
                assignedName,
                x.Status);
        }).ToList();

        return new PagedResult<ActivityListItem>(listItems, p.Page, p.PageSize, total);
    }

    public async Task<Result<ActivityDetail>> GetAsync(int id, CancellationToken ct = default)
    {
        if (!_currentUser.Can(Permissions.ActivitiesView))
        {
            return Result<ActivityDetail>.Fail(ErrorKind.Forbidden);
        }

        var scope = await _ownerScope.ResolveAsync(ct);
        var act = await _db.Activities.AsNoTracking().InScope(scope)
            .Include(x => x.Customer)
            .Include(x => x.Lead)
            .Include(x => x.Opportunity)
            .FirstOrDefaultAsync(x => x.Id == id, ct);

        if (act is null)
        {
            return Result<ActivityDetail>.Fail(ErrorKind.NotFound);
        }

        return Result<ActivityDetail>.Ok(await MapDetailAsync(act, ct));
    }

    public async Task<Result<ActivityDetail>> CreateAsync(CreateActivityCommand c, CancellationToken ct = default)
    {
        if (!_currentUser.Can(Permissions.ActivitiesCreate))
        {
            return Result<ActivityDetail>.Fail(ErrorKind.Forbidden);
        }

        var relatedCount = c.Related?.Count ?? 0;
        var errors = ActivityRules.Validate(c.Subject, c.Description, relatedCount).ToList();
        if (errors.Count > 0)
        {
            return Result<ActivityDetail>.Fail(ErrorKind.Validation, errors);
        }

        if (c.Related is null || c.Related.Count == 0)
        {
            return Result<ActivityDetail>.Fail(ErrorKind.Validation, [new FieldError("Related", "Select at least one related record.")]);
        }

        if (c.Related.Select(r => r.Type).Distinct().Count() != c.Related.Count)
        {
            return Result<ActivityDetail>.Fail(ErrorKind.Validation, [new FieldError("Related", "Cannot have duplicate related types.")]);
        }

        var scope = await _ownerScope.ResolveAsync(ct);
        int? customerId = null;
        int? leadId = null;
        int? opportunityId = null;

        foreach (var rel in c.Related)
        {
            switch (rel.Type)
            {
                case RelatedType.Customer:
                    var cust = await _db.Customers.AsNoTracking().InScope(scope).FirstOrDefaultAsync(x => x.Id == rel.Id, ct);
                    if (cust is null)
                    {
                        return Result<ActivityDetail>.Fail(ErrorKind.Validation, [new FieldError("Related", "Choose an active customer.")]);
                    }
                    customerId = cust.Id;
                    break;

                case RelatedType.Lead:
                    var lead = await _db.Leads.AsNoTracking().InScope(scope).FirstOrDefaultAsync(x => x.Id == rel.Id, ct);
                    if (lead is null)
                    {
                        return Result<ActivityDetail>.Fail(ErrorKind.Validation, [new FieldError("Related", "Choose an active lead.")]);
                    }
                    leadId = lead.Id;
                    break;

                case RelatedType.Opportunity:
                    var opp = await _db.Opportunities.AsNoTracking().InScope(scope).FirstOrDefaultAsync(x => x.Id == rel.Id, ct);
                    if (opp is null)
                    {
                        return Result<ActivityDetail>.Fail(ErrorKind.Validation, [new FieldError("Related", "Choose an active opportunity.")]);
                    }
                    opportunityId = opp.Id;
                    break;
            }
        }

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
                return Result<ActivityDetail>.Fail(ErrorKind.Validation, [new FieldError("AssignedToId", "Choose an active assignable user.")]);
            }
        }

        var activity = Activity.Create(
            c.Type,
            c.Subject.Trim(),
            c.Description?.Trim(),
            c.ActivityDateUtc,
            customerId,
            leadId,
            opportunityId,
            assignedToId,
            c.Status);

        _db.Activities.Add(activity);
        await _db.SaveChangesAsync(ct);

        return await GetAsync(activity.Id, ct);
    }

    public async Task<Result<ActivityDetail>> UpdateAsync(int id, UpdateActivityCommand c, CancellationToken ct = default)
    {
        if (!_currentUser.Can(Permissions.ActivitiesEdit))
        {
            return Result<ActivityDetail>.Fail(ErrorKind.Forbidden);
        }

        var scope = await _ownerScope.ResolveAsync(ct);
        var activity = await _db.Activities.InScope(scope).FirstOrDefaultAsync(x => x.Id == id, ct);
        if (activity is null)
        {
            return Result<ActivityDetail>.Fail(ErrorKind.NotFound);
        }

        if (c.RowVersion.Length > 0 && !c.RowVersion.SequenceEqual(activity.RowVersion))
        {
            return ConcurrencyHelper.Conflict<ActivityDetail>("Activity");
        }

        var relatedCount = c.Related?.Count ?? 0;
        var errors = ActivityRules.Validate(c.Subject, c.Description, relatedCount).ToList();
        if (errors.Count > 0)
        {
            return Result<ActivityDetail>.Fail(ErrorKind.Validation, errors);
        }

        if (c.Related is null || c.Related.Count == 0)
        {
            return Result<ActivityDetail>.Fail(ErrorKind.Validation, [new FieldError("Related", "Select at least one related record.")]);
        }

        if (c.Related.Select(r => r.Type).Distinct().Count() != c.Related.Count)
        {
            return Result<ActivityDetail>.Fail(ErrorKind.Validation, [new FieldError("Related", "Cannot have duplicate related types.")]);
        }

        int? customerId = null;
        int? leadId = null;
        int? opportunityId = null;

        foreach (var rel in c.Related)
        {
            switch (rel.Type)
            {
                case RelatedType.Customer:
                    var cust = await _db.Customers.AsNoTracking().InScope(scope).FirstOrDefaultAsync(x => x.Id == rel.Id, ct);
                    if (cust is null)
                    {
                        return Result<ActivityDetail>.Fail(ErrorKind.Validation, [new FieldError("Related", "Choose an active customer.")]);
                    }
                    customerId = cust.Id;
                    break;

                case RelatedType.Lead:
                    var lead = await _db.Leads.AsNoTracking().InScope(scope).FirstOrDefaultAsync(x => x.Id == rel.Id, ct);
                    if (lead is null)
                    {
                        return Result<ActivityDetail>.Fail(ErrorKind.Validation, [new FieldError("Related", "Choose an active lead.")]);
                    }
                    leadId = lead.Id;
                    break;

                case RelatedType.Opportunity:
                    var opp = await _db.Opportunities.AsNoTracking().InScope(scope).FirstOrDefaultAsync(x => x.Id == rel.Id, ct);
                    if (opp is null)
                    {
                        return Result<ActivityDetail>.Fail(ErrorKind.Validation, [new FieldError("Related", "Choose an active opportunity.")]);
                    }
                    opportunityId = opp.Id;
                    break;
            }
        }

        string assignedToId = activity.AssignedToId;
        if (_currentUser.Role != Roles.SalesExecutive && !string.IsNullOrWhiteSpace(c.AssignedToId))
        {
            var assignable = await _userDirectory.ListAssignableOwnersAsync(ct);
            if (!assignable.Any(u => u.Id == c.AssignedToId))
            {
                return Result<ActivityDetail>.Fail(ErrorKind.Validation, [new FieldError("AssignedToId", "Choose an active assignable user.")]);
            }
            assignedToId = c.AssignedToId;
        }

        activity.Update(
            c.Type,
            c.Subject.Trim(),
            c.Description?.Trim(),
            c.ActivityDateUtc,
            customerId,
            leadId,
            opportunityId,
            assignedToId,
            c.Status);

        if (c.RowVersion.Length > 0)
        {
            _db.Entry(activity).Property(x => x.RowVersion).OriginalValue = c.RowVersion;
        }

        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            return ConcurrencyHelper.Conflict<ActivityDetail>("Activity");
        }

        return await GetAsync(activity.Id, ct);
    }

    public async Task<Result> DeleteAsync(int id, CancellationToken ct = default)
    {
        if (!_currentUser.Can(Permissions.ActivitiesDelete))
        {
            return Result.Fail(ErrorKind.Forbidden);
        }

        var scope = await _ownerScope.ResolveAsync(ct);
        var activity = await _db.Activities.InScope(scope).FirstOrDefaultAsync(x => x.Id == id, ct);
        if (activity is null)
        {
            return Result.Fail(ErrorKind.NotFound);
        }

        activity.SoftDelete(_currentUser.UserId ?? "system", _clock.UtcNow);
        await _db.SaveChangesAsync(ct);
        return Result.Ok();
    }

    private async Task<ActivityDetail> MapDetailAsync(Activity a, CancellationToken ct)
    {
        var user = await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == a.AssignedToId, ct);
        var assignedName = user?.FullName ?? a.AssignedToId;

        return new ActivityDetail(
            a.Id,
            a.Type,
            a.Subject,
            a.Description,
            a.ActivityDate,
            a.CustomerId,
            a.Customer?.Name,
            a.LeadId,
            a.Lead?.Name,
            a.OpportunityId,
            a.Opportunity?.Name,
            a.AssignedToId,
            assignedName,
            a.Status,
            a.CreatedDate,
            a.RowVersion);
    }
}

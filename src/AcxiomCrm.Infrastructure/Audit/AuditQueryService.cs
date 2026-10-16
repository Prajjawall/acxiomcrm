using System.Text.Json;
using AcxiomCrm.Application.Abstractions;
using AcxiomCrm.Application.Audit;
using AcxiomCrm.Application.Common;
using AcxiomCrm.Domain.Entities;
using AcxiomCrm.Domain.Validation;
using AcxiomCrm.Infrastructure.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AcxiomCrm.Infrastructure.Audit;

public sealed class AuditQueryService : IAuditQueryService
{
    private readonly ICrmDbContext _context;
    private readonly ICurrentUser _currentUser;
    private readonly TimeZoneInfo _timeZoneInfo;

    public AuditQueryService(
        ICrmDbContext context,
        ICurrentUser currentUser,
        IOptions<BusinessOptions> options)
    {
        _context = context;
        _currentUser = currentUser;

        var zone = options.Value.TimeZone ?? "Asia/Kolkata";
        try
        {
            _timeZoneInfo = TimeZoneInfo.FindSystemTimeZoneById(zone);
        }
        catch
        {
            _timeZoneInfo = TimeZoneInfo.Utc;
        }
    }

    private bool IsAdmin => string.Equals(_currentUser.Role, "Admin", StringComparison.OrdinalIgnoreCase);

    public async Task<PagedResult<AuditListItem>> SearchAsync(AuditFilter filter, PageQuery page, CancellationToken ct = default)
    {
        if (!IsAdmin)
        {
            throw new UnauthorizedAccessException("Forbidden: audit view permission required.");
        }

        IQueryable<AuditLog> query = _context.AuditLogs.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(filter.UserId))
        {
            query = query.Where(x => x.UserId == filter.UserId);
        }

        if (!string.IsNullOrWhiteSpace(filter.Module))
        {
            query = query.Where(x => x.Module == filter.Module);
        }

        if (!string.IsNullOrWhiteSpace(filter.Action))
        {
            query = query.Where(x => x.Action == filter.Action);
        }

        if (filter.Result.HasValue)
        {
            query = query.Where(x => x.Result == filter.Result.Value);
        }

        if (!string.IsNullOrWhiteSpace(filter.EntityName))
        {
            query = query.Where(x => x.EntityName == filter.EntityName);
        }

        if (!string.IsNullOrWhiteSpace(filter.RecordId))
        {
            query = query.Where(x => x.RecordId == filter.RecordId);
        }

        if (filter.From.HasValue)
        {
            var localStart = filter.From.Value.ToDateTime(TimeOnly.MinValue);
            var fromUtc = TimeZoneInfo.ConvertTimeToUtc(localStart, _timeZoneInfo);
            query = query.Where(x => x.CreatedDate >= fromUtc);
        }

        if (filter.To.HasValue)
        {
            var localEndNextDay = filter.To.Value.AddDays(1).ToDateTime(TimeOnly.MinValue);
            var toUtc = TimeZoneInfo.ConvertTimeToUtc(localEndNextDay, _timeZoneInfo);
            query = query.Where(x => x.CreatedDate < toUtc);
        }

        if (!string.IsNullOrWhiteSpace(filter.Text))
        {
            var text = filter.Text.Trim();
            query = query.Where(x =>
                (x.Details != null && x.Details.Contains(text)) ||
                (x.UserName != null && x.UserName.Contains(text)));
        }

        query = query.OrderByDescending(x => x.CreatedDate).ThenByDescending(x => x.AuditLogId);

        var total = await query.CountAsync(ct);
        var items = await query
            .Skip(page.Skip)
            .Take(page.Take)
            .Select(x => new AuditListItem(
                x.AuditLogId,
                x.CreatedDate,
                x.UserName,
                x.Module,
                x.Action,
                x.EntityName,
                x.RecordId,
                x.Result,
                x.IpAddress,
                x.Details))
            .ToListAsync(ct);

        return new PagedResult<AuditListItem>(items, page.Page, page.PageSize, total);
    }

    public async Task<Result<AuditDetail>> GetAsync(long id, CancellationToken ct = default)
    {
        if (!IsAdmin)
        {
            return Result<AuditDetail>.Fail(ErrorKind.Forbidden, new FieldError("User", "Forbidden: audit view permission required."));
        }

        var log = await _context.AuditLogs.AsNoTracking().FirstOrDefaultAsync(x => x.AuditLogId == id, ct);
        if (log == null)
        {
            return Result<AuditDetail>.Fail(ErrorKind.NotFound, new FieldError("Id", "Audit log not found."));
        }

        var header = new AuditListItem(
            log.AuditLogId,
            log.CreatedDate,
            log.UserName,
            log.Module,
            log.Action,
            log.EntityName,
            log.RecordId,
            log.Result,
            log.IpAddress,
            log.Details);

        var changes = ParseChanges(log.OldValue, log.NewValue);

        return Result<AuditDetail>.Ok(new AuditDetail(header, changes, log.CorrelationId));
    }

    public async Task<IReadOnlyList<string>> ListModulesAsync(CancellationToken ct = default)
    {
        return await _context.AuditLogs
            .AsNoTracking()
            .Select(x => x.Module)
            .Distinct()
            .OrderBy(x => x)
            .ToListAsync(ct);
    }

    public Task<PagedResult<AuditListItem>> ForRecordAsync(string entityName, string recordId, PageQuery page, CancellationToken ct = default)
    {
        return SearchAsync(new AuditFilter(EntityName: entityName, RecordId: recordId), page, ct);
    }

    private static List<AuditChange> ParseChanges(string? oldValueJson, string? newValueJson)
    {
        var changes = new List<AuditChange>();
        var oldDict = ParseJsonDict(oldValueJson);
        var newDict = ParseJsonDict(newValueJson);

        var allKeys = new HashSet<string>(oldDict.Keys, StringComparer.OrdinalIgnoreCase);
        foreach (var k in newDict.Keys)
        {
            allKeys.Add(k);
        }

        foreach (var key in allKeys.OrderBy(k => k, StringComparer.OrdinalIgnoreCase))
        {
            oldDict.TryGetValue(key, out var oldVal);
            newDict.TryGetValue(key, out var newVal);
            changes.Add(new AuditChange(key, oldVal, newVal));
        }

        return changes;
    }

    private static Dictionary<string, string?> ParseJsonDict(string? json)
    {
        var dict = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(json))
        {
            return dict;
        }

        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind == JsonValueKind.Object)
            {
                foreach (var prop in doc.RootElement.EnumerateObject())
                {
                    dict[prop.Name] = prop.Value.ValueKind switch
                    {
                        JsonValueKind.Null => null,
                        JsonValueKind.String => prop.Value.GetString(),
                        _ => prop.Value.GetRawText()
                    };
                }
            }
        }
        catch
        {
            // If json is malformed or raw string, ignore
        }

        return dict;
    }
}

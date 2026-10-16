using System.Text.RegularExpressions;
using AcxiomCrm.Application.Abstractions;
using AcxiomCrm.Domain.Audit;
using AcxiomCrm.Domain.Entities;
using AcxiomCrm.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AcxiomCrm.Infrastructure.Audit;

public class AuditWriter : IAuditWriter
{
    private static readonly Regex SensitiveDetailsPattern = new(@"(?i)(password|token|secret)\s*=", RegexOptions.Compiled);

    private readonly IDbContextFactory<CrmDbContext> _contextFactory;
    private readonly ICurrentUser _currentUser;
    private readonly IClock _clock;

    public AuditWriter(IDbContextFactory<CrmDbContext> contextFactory, ICurrentUser currentUser, IClock clock)
    {
        _contextFactory = contextFactory;
        _currentUser = currentUser;
        _clock = clock;
    }

    public async Task WriteAsync(AuditEntry entry, CancellationToken ct = default)
    {
        if (!string.IsNullOrWhiteSpace(entry.Details) && SensitiveDetailsPattern.IsMatch(entry.Details))
        {
            throw new ArgumentException("Sensitive keys (password/token/secret) are not permitted in audit details.", nameof(entry));
        }

        await using var context = await _contextFactory.CreateDbContextAsync(ct);

        string? userId = null;
        string? userName = null;

        if (!string.IsNullOrWhiteSpace(entry.UserIdOverride))
        {
            userId = entry.UserIdOverride;
            var user = await context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId, ct);
            userName = user?.UserName ?? userId;
        }
        else if (_currentUser.IsAuthenticated)
        {
            userId = _currentUser.UserId;
            userName = _currentUser.UserName;
        }

        var details = entry.Details;
        if (details != null && details.Length > 1000)
        {
            details = details[..997] + "...";
        }

        var module = InferModule(entry.EntityName);

        var log = new AuditLog
        {
            CreatedDate = _clock.UtcNow,
            UserId = userId,
            UserName = userName,
            Module = module,
            Action = entry.Action,
            EntityName = entry.EntityName,
            RecordId = entry.RecordId,
            Result = entry.Result,
            OldValue = entry.OldValue,
            NewValue = entry.NewValue,
            Details = details,
            IpAddress = Truncate(_currentUser.IpAddress, 45),
            CorrelationId = Truncate(_currentUser.CorrelationId, 64)
        };

        context.AuditLogs.Add(log);
        await context.SaveChangesAsync(ct);
    }

    private static string InferModule(string entityName)
    {
        return entityName.ToLowerInvariant() switch
        {
            "user" or "auth" or "applicationuser" or "authentication" => AuditModules.Authentication,
            "customer" or "customers" => AuditModules.Customers,
            "lead" or "leads" => AuditModules.Leads,
            "opportunity" or "opportunities" => AuditModules.Opportunities,
            "followup" or "followups" => AuditModules.FollowUps,
            "activity" or "activities" => AuditModules.Activities,
            "role" or "applicationrole" or "users" => AuditModules.Users,
            "security" => AuditModules.Security,
            _ => entityName
        };
    }

    private static string? Truncate(string? value, int maxLength)
    {
        if (string.IsNullOrEmpty(value) || value.Length <= maxLength)
        {
            return value;
        }
        return value[..maxLength];
    }
}

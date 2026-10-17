using AcxiomCrm.Application.Abstractions;
using AcxiomCrm.Domain.Audit;
using AcxiomCrm.Domain.Common;
using AcxiomCrm.Domain.Entities;
using AcxiomCrm.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Storage;

namespace AcxiomCrm.Infrastructure.Audit;

public sealed class AuditSaveChangesInterceptor(IClock clock, ICurrentUser? currentUser = null, IAuditActionHint? actionHint = null) : SaveChangesInterceptor
{
    private sealed record PendingAudit(EntityEntry Entry, AuditLog Log);

    private readonly List<PendingAudit> _pending = [];
    private IDbContextTransaction? _localTransaction;
    private bool _isSavingAuditLogs;

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        if (eventData.Context != null && !_isSavingAuditLogs)
        {
            ProcessSaving(eventData.Context, async: false).GetAwaiter().GetResult();
        }
        return base.SavingChanges(eventData, result);
    }

    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        if (eventData.Context != null && !_isSavingAuditLogs)
        {
            await ProcessSaving(eventData.Context, async: true);
        }
        return await base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    public override int SavedChanges(SaveChangesCompletedEventData eventData, int result)
    {
        if (eventData.Context != null && !_isSavingAuditLogs)
        {
            ProcessSaved(eventData.Context, async: false).GetAwaiter().GetResult();
        }
        return base.SavedChanges(eventData, result);
    }

    public override async ValueTask<int> SavedChangesAsync(
        SaveChangesCompletedEventData eventData,
        int result,
        CancellationToken cancellationToken = default)
    {
        if (eventData.Context != null && !_isSavingAuditLogs)
        {
            await ProcessSaved(eventData.Context, async: true);
        }
        return await base.SavedChangesAsync(eventData, result, cancellationToken);
    }

    public override void SaveChangesFailed(DbContextErrorEventData eventData)
    {
        CleanupTransaction(rollback: true);
        base.SaveChangesFailed(eventData);
    }

    public override Task SaveChangesFailedAsync(DbContextErrorEventData eventData, CancellationToken cancellationToken = default)
    {
        CleanupTransaction(rollback: true);
        return base.SaveChangesFailedAsync(eventData, cancellationToken);
    }

    private async Task ProcessSaving(DbContext context, bool async)
    {
        _pending.Clear();

        // 1. Throw if AuditLog is being modified or deleted
        foreach (var entry in context.ChangeTracker.Entries<AuditLog>())
        {
            if (entry.State is EntityState.Modified or EntityState.Deleted)
            {
                throw new InvalidOperationException("Audit logs cannot be modified or deleted.");
            }
        }

        var now = clock.UtcNow;
        var userId = string.IsNullOrWhiteSpace(currentUser?.UserId) ? "system" : currentUser.UserId;
        var userName = string.IsNullOrWhiteSpace(currentUser?.UserName)
            ? (string.IsNullOrWhiteSpace(currentUser?.UserId) ? "system" : null)
            : currentUser.UserName;

        var entries = context.ChangeTracker.Entries().ToList();

        // 2. Timestamps and soft-deletes
        foreach (var entry in entries)
        {
            if (entry.Entity is IHasTimestamps)
            {
                if (entry.State == EntityState.Added)
                {
                    entry.Property("CreatedDate").CurrentValue = now;
                    entry.Property("CreatedBy").CurrentValue = userId;
                }
                else if (entry.State == EntityState.Modified)
                {
                    entry.Property("ModifiedDate").CurrentValue = now;
                    entry.Property("ModifiedBy").CurrentValue = userId;
                }
            }

            if (entry.Entity is ISoftDeletable && entry.State == EntityState.Deleted)
            {
                entry.State = EntityState.Modified;
                entry.Property("IsDeleted").CurrentValue = true;
                entry.Property("DeletedDate").CurrentValue = now;
                entry.Property("DeletedBy").CurrentValue = userId;
            }
        }

        // 3. Automatic auditing for IAuditedEntity
        var auditedEntries = entries.Where(e => e.Entity is IAuditedEntity && e.Entity is not AuditLog).ToList();

        foreach (var entry in auditedEntries)
        {
            var entityName = entry.Metadata.ClrType.Name;
            var module = AuditModules.ForEntity(entityName);

            if (entry.State == EntityState.Added)
            {
                var newValues = GetNonSensitiveProperties(entry, useOriginal: false);
                var auditLog = new AuditLog
                {
                    CreatedDate = now,
                    UserId = userId,
                    UserName = userName,
                    Module = module,
                    Action = AuditActions.Create,
                    EntityName = entityName,
                    Result = AuditResult.Success,
                    NewValue = AuditSnapshot.Serialize(newValues),
                    IpAddress = currentUser?.IpAddress,
                    CorrelationId = currentUser?.CorrelationId
                };

                var pkProp = entry.Properties.FirstOrDefault(p => p.Metadata.IsPrimaryKey());
                var pkValue = pkProp?.CurrentValue;
                var isPkGenerated = pkProp != null && (
                    pkProp.IsTemporary ||
                    pkValue is null or 0 or 0L or "" ||
                    (pkValue is long l && l <= 0) ||
                    (pkValue is int i && i <= 0));

                if (isPkGenerated)
                {
                    _pending.Add(new PendingAudit(entry, auditLog));
                }
                else
                {
                    auditLog.RecordId = pkValue?.ToString();
                    context.Set<AuditLog>().Add(auditLog);
                }
            }
            else if (entry.State == EntityState.Modified)
            {
                var isSoftDelete = entry.Entity is ISoftDeletable &&
                                   entry.Property("IsDeleted").IsModified &&
                                   entry.Property("IsDeleted").CurrentValue is true;

                var pkProp = entry.Properties.FirstOrDefault(p => p.Metadata.IsPrimaryKey());
                var recordId = pkProp?.CurrentValue?.ToString();

                if (isSoftDelete)
                {
                    var oldValues = GetNonSensitiveProperties(entry, useOriginal: true);
                    var auditLog = new AuditLog
                    {
                        CreatedDate = now,
                        UserId = userId,
                        UserName = userName,
                        Module = module,
                        Action = AuditActions.Delete,
                        EntityName = entityName,
                        RecordId = recordId,
                        Result = AuditResult.Success,
                        OldValue = AuditSnapshot.Serialize(oldValues),
                        IpAddress = currentUser?.IpAddress,
                        CorrelationId = currentUser?.CorrelationId
                    };
                    context.Set<AuditLog>().Add(auditLog);
                }
                else
                {
                    var changedOld = new Dictionary<string, object?>();
                    var changedNew = new Dictionary<string, object?>();

                    foreach (var prop in entry.Properties)
                    {
                        var propName = prop.Metadata.Name;
                        if (propName is "ModifiedDate" or "ModifiedBy" or "RowVersion")
                        {
                            continue;
                        }

                        if (SensitiveProperties.IsSensitive(propName))
                        {
                            continue;
                        }

                        if (!Equals(prop.OriginalValue, prop.CurrentValue))
                        {
                            changedOld[propName] = prop.OriginalValue;
                            changedNew[propName] = prop.CurrentValue;
                        }
                    }

                    if (changedOld.Count > 0)
                    {
                        var auditLog = new AuditLog
                        {
                            CreatedDate = now,
                            UserId = userId,
                            UserName = userName,
                            Module = module,
                            Action = actionHint?.Take() ?? AuditActions.Update,
                            EntityName = entityName,
                            RecordId = recordId,
                            Result = AuditResult.Success,
                            OldValue = AuditSnapshot.Serialize(changedOld),
                            NewValue = AuditSnapshot.Serialize(changedNew),
                            IpAddress = currentUser?.IpAddress,
                            CorrelationId = currentUser?.CorrelationId
                        };
                        context.Set<AuditLog>().Add(auditLog);
                    }
                }
            }
            else if (entry.State == EntityState.Deleted)
            {
                var pkProp = entry.Properties.FirstOrDefault(p => p.Metadata.IsPrimaryKey());
                var recordId = pkProp?.CurrentValue?.ToString();
                var oldValues = GetNonSensitiveProperties(entry, useOriginal: true);

                var auditLog = new AuditLog
                {
                    CreatedDate = now,
                    UserId = userId,
                    UserName = userName,
                    Module = module,
                    Action = AuditActions.Delete,
                    EntityName = entityName,
                    RecordId = recordId,
                    Result = AuditResult.Success,
                    OldValue = AuditSnapshot.Serialize(oldValues),
                    IpAddress = currentUser?.IpAddress,
                    CorrelationId = currentUser?.CorrelationId
                };
                context.Set<AuditLog>().Add(auditLog);
            }
        }

        // If we have pending store-generated key audits and no existing transaction, begin one
        if (_pending.Count > 0 && context.Database.CurrentTransaction == null)
        {
            if (async)
            {
                _localTransaction = await context.Database.BeginTransactionAsync();
            }
            else
            {
                _localTransaction = context.Database.BeginTransaction();
            }
        }
    }

    private async Task ProcessSaved(DbContext context, bool async)
    {
        if (_pending.Count == 0)
        {
            return;
        }

        var toSave = _pending.ToList();
        _pending.Clear();

        _isSavingAuditLogs = true;
        try
        {
            foreach (var p in toSave)
            {
                var pkProp = p.Entry.Properties.FirstOrDefault(x => x.Metadata.IsPrimaryKey());
                p.Log.RecordId = pkProp?.CurrentValue?.ToString() ?? "0";
                context.Set<AuditLog>().Add(p.Log);
            }

            if (async)
            {
                await context.SaveChangesAsync();
            }
            else
            {
                context.SaveChanges();
            }

            if (_localTransaction != null)
            {
                if (async)
                {
                    await _localTransaction.CommitAsync();
                    await _localTransaction.DisposeAsync();
                }
                else
                {
                    _localTransaction.Commit();
                    _localTransaction.Dispose();
                }
                _localTransaction = null;
            }
        }
        finally
        {
            _isSavingAuditLogs = false;
        }
    }

    private void CleanupTransaction(bool rollback)
    {
        _pending.Clear();
        if (_localTransaction != null)
        {
            try
            {
                if (rollback)
                {
                    _localTransaction.Rollback();
                }
                _localTransaction.Dispose();
            }
            catch
            {
                // Ignored during failure cleanup
            }
            finally
            {
                _localTransaction = null;
            }
        }
    }

    private static Dictionary<string, object?> GetNonSensitiveProperties(EntityEntry entry, bool useOriginal)
    {
        var dict = new Dictionary<string, object?>();
        foreach (var prop in entry.Properties)
        {
            var name = prop.Metadata.Name;
            if (SensitiveProperties.IsSensitive(name) || name == "RowVersion")
            {
                continue;
            }

            var val = useOriginal ? prop.OriginalValue : prop.CurrentValue;
            dict[name] = val;
        }
        return dict;
    }
}

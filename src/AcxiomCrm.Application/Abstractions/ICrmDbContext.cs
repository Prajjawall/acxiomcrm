using AcxiomCrm.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace AcxiomCrm.Application.Abstractions;

public interface ICrmDbContext
{
    DbSet<AuditLog> AuditLogs { get; }
    DbSet<Customer> Customers { get; }
    IQueryable<AcxiomCrm.Application.Users.UserSummary> Users { get; }
    Task<int> SaveChangesAsync(CancellationToken ct = default);
    Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken ct = default);
}

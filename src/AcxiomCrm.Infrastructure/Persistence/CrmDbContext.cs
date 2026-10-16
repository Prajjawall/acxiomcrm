using AcxiomCrm.Application.Abstractions;
using AcxiomCrm.Domain.Entities;
using AcxiomCrm.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace AcxiomCrm.Infrastructure.Persistence;

public class CrmDbContext : IdentityDbContext<ApplicationUser, ApplicationRole, string>, ICrmDbContext
{
    public CrmDbContext(DbContextOptions options) : base(options)
    {
    }
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    IQueryable<AcxiomCrm.Application.Users.UserSummary> ICrmDbContext.Users => Set<AcxiomCrm.Application.Users.UserSummary>();

    public Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken ct = default) =>
        Database.BeginTransactionAsync(ct);

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.ApplyConfigurationsFromAssembly(typeof(CrmDbContext).Assembly);
    }
}

using AcxiomCrm.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AcxiomCrm.TestSupport.Fakes;

public class TestCrmDbContext(DbContextOptions<TestCrmDbContext> options) : CrmDbContext(options)
{
    public DbSet<AuditedThing> AuditedThings => Set<AuditedThing>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.Entity<AuditedThing>(b =>
        {
            b.HasKey(x => x.Id);
            b.Property(x => x.Name).HasMaxLength(100).IsRequired();
            b.Property(x => x.Description).HasMaxLength(500);
            b.Property(x => x.PasswordHash).HasMaxLength(200);
            b.Property(x => x.RowVersion).IsRowVersion();
        });
    }
}

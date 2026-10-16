using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace AcxiomCrm.Infrastructure.Persistence;

public sealed class CrmDbContextFactory : IDesignTimeDbContextFactory<CrmDbContext>
{
    public CrmDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<CrmDbContext>();
        optionsBuilder.UseSqlServer("Server=(localdb)\\mssqllocaldb;Database=AcxiomCrm_Dev;Trusted_Connection=True;MultipleActiveResultSets=true");
        return new CrmDbContext(optionsBuilder.Options);
    }
}

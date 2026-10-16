using Microsoft.EntityFrameworkCore.Storage;

namespace AcxiomCrm.Application.Abstractions;

public interface ICrmDbContext
{
    Task<int> SaveChangesAsync(CancellationToken ct = default);
    Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken ct = default);
}

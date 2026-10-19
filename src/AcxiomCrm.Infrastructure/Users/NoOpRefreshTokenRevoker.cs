using AcxiomCrm.Application.Abstractions;

namespace AcxiomCrm.Infrastructure.Users;

public sealed class NoOpRefreshTokenRevoker : IRefreshTokenRevoker
{
    public Task RevokeAllAsync(string userId, CancellationToken ct = default) => Task.CompletedTask;
}

namespace AcxiomCrm.Application.Abstractions;

public interface IRefreshTokenRevoker
{
    Task RevokeAllAsync(string userId, CancellationToken ct = default);
}

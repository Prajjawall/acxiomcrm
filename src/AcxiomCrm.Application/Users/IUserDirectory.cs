namespace AcxiomCrm.Application.Users;

public sealed record UserOption(string Id, string Name, string Role);

public interface IUserDirectory
{
    Task<IReadOnlyList<UserOption>> ListAssignableOwnersAsync(CancellationToken ct = default);
    Task<UserOption?> GetOptionAsync(string userId, CancellationToken ct = default);
}

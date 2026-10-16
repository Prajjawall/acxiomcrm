using AcxiomCrm.Application.Abstractions;

namespace AcxiomCrm.TestSupport;

public sealed class FakeCurrentUser : ICurrentUser
{
    public FakeCurrentUser(string userId, string role, string? userName = null, string? ip = "127.0.0.1")
    {
        UserId = userId;
        Role = role;
        UserName = userName ?? userId;
        IpAddress = ip;
        IsAuthenticated = !string.IsNullOrEmpty(userId);
        CorrelationId = Guid.NewGuid().ToString("N");
    }

    public string? UserId { get; }

    public string? UserName { get; }

    public string? Role { get; }

    public bool IsAuthenticated { get; }

    public string? IpAddress { get; }

    public string? CorrelationId { get; }
}

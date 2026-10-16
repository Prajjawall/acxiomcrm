using AcxiomCrm.Application.Abstractions;

namespace AcxiomCrm.TestSupport;

public sealed class FakeCurrentUser : ICurrentUser
{
    public FakeCurrentUser()
    {
        IsAuthenticated = false;
        CorrelationId = Guid.NewGuid().ToString("N");
    }

    public FakeCurrentUser(string userId, string role, string? userName = null, string? ip = "127.0.0.1", string? correlationId = null)
    {
        UserId = userId;
        Role = role;
        UserName = userName ?? userId;
        IpAddress = ip;
        IsAuthenticated = !string.IsNullOrEmpty(userId);
        CorrelationId = correlationId ?? Guid.NewGuid().ToString("N");
    }

    public string? UserId { get; set; }

    public string? UserName { get; set; }

    public string? Role { get; set; }

    public bool IsAuthenticated { get; set; }

    public string? IpAddress { get; set; }

    public string? CorrelationId { get; set; }
}

namespace AcxiomCrm.Application.Abstractions;

public interface ICurrentUser
{
    string? UserId { get; }
    string? UserName { get; }
    string? Role { get; }
    bool IsAuthenticated { get; }
    string? IpAddress { get; }
    string? CorrelationId { get; }
}

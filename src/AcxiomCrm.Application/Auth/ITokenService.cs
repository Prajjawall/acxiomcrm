namespace AcxiomCrm.Application.Auth;

public sealed record GeneratedToken(
    string AccessToken,
    int ExpiresInSeconds,
    string TokenType = "Bearer");

public interface ITokenService
{
    GeneratedToken GenerateAccessToken(string userId, string userName, string email, string role);
}

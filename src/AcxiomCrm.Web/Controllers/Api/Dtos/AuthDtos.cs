namespace AcxiomCrm.Web.Controllers.Api.Dtos;

public sealed record LoginRequest(string? UserNameOrEmail, string? Password);

public sealed record CurrentUserDto(
    string Id,
    string FullName,
    string UserName,
    string Email,
    string Role,
    IReadOnlyList<string> Permissions);

public sealed record AuthTokensDto(
    string AccessToken,
    int ExpiresInSeconds,
    string? RefreshToken,
    string TokenType,
    CurrentUserDto User);

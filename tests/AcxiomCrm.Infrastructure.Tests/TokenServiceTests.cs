using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using AcxiomCrm.Application.Abstractions;
using AcxiomCrm.Infrastructure.Auth;
using AcxiomCrm.Infrastructure.Options;
using Microsoft.IdentityModel.Tokens;
using NSubstitute;
using Shouldly;
using MsOptions = Microsoft.Extensions.Options.Options;

namespace AcxiomCrm.Infrastructure.Tests;

public class TokenServiceTests
{
    private readonly IClock _clock = Substitute.For<IClock>();
    private readonly JwtOptions _options = new()
    {
        Enabled = true,
        SigningKey = "VeryLongSecretKeyForTestingJwtTokens1234567890!",
        Issuer = "AcxiomCrm",
        Audience = "AcxiomCrm",
        AccessMinutes = 60
    };

    public TokenServiceTests()
    {
        _clock.UtcNow.Returns(new DateTime(2026, 10, 20, 12, 0, 0, DateTimeKind.Utc));
    }

    [Fact]
    public void GenerateAccessToken_ReturnsValidToken_WithExpectedClaimsAndExpiry()
    {
        // Arrange
        var service = new TokenService(MsOptions.Create(_options), _clock);

        // Act
        var result = service.GenerateAccessToken("user-1", "alice", "alice@example.com", "SalesExecutive");

        // Assert
        result.ShouldNotBeNull();
        result.TokenType.ShouldBe("Bearer");
        result.ExpiresInSeconds.ShouldBe(3600);
        result.AccessToken.ShouldNotBeNullOrWhiteSpace();

        // Validate token
        var handler = new JwtSecurityTokenHandler();
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.SigningKey));

        var principal = handler.ValidateToken(result.AccessToken, new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = _options.Issuer,
            ValidateAudience = true,
            ValidAudience = _options.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = key,
            ValidateLifetime = true,
            LifetimeValidator = (notBefore, expires, securityToken, validationParameters) =>
                notBefore <= new DateTime(2026, 10, 20, 12, 0, 0, DateTimeKind.Utc) &&
                expires >= new DateTime(2026, 10, 20, 12, 0, 0, DateTimeKind.Utc),
            ClockSkew = TimeSpan.Zero
        }, out var validatedToken);

        principal.FindFirst(ClaimTypes.NameIdentifier)?.Value.ShouldBe("user-1");
        principal.FindFirst(ClaimTypes.Name)?.Value.ShouldBe("alice");
        principal.FindFirst(ClaimTypes.Email)?.Value.ShouldBe("alice@example.com");
        principal.FindFirst(ClaimTypes.Role)?.Value.ShouldBe("SalesExecutive");

        validatedToken.ValidTo.ShouldBe(new DateTime(2026, 10, 20, 13, 0, 0, DateTimeKind.Utc));
    }
}

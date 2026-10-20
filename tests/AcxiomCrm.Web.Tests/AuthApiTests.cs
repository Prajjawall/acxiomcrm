using AcxiomCrm.Application.Abstractions;
using AcxiomCrm.Application.Auth;
using AcxiomCrm.Application.Common;
using AcxiomCrm.Application.Users;
using AcxiomCrm.Domain.Security;
using AcxiomCrm.Web.Controllers.Api;
using AcxiomCrm.Web.Controllers.Api.Dtos;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using Shouldly;

namespace AcxiomCrm.Web.Tests;

public class AuthApiTests
{
    private static readonly DateTime FixedNowUtc = new(2026, 10, 20, 12, 0, 0, DateTimeKind.Utc);
    private readonly IAuthService _authService = Substitute.For<IAuthService>();
    private readonly ITokenService _tokenService = Substitute.For<ITokenService>();
    private readonly IUserAdminService _userAdminService = Substitute.For<IUserAdminService>();
    private readonly ICurrentUser _currentUser = Substitute.For<ICurrentUser>();
    private readonly IClock _clock = Substitute.For<IClock>();

    private AuthApiController CreateController()
    {
        var httpContext = new DefaultHttpContext();
        httpContext.TraceIdentifier = "trace-12345";

        var controller = new AuthApiController(
            _authService,
            _tokenService,
            _userAdminService,
            _currentUser,
            _clock)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = httpContext
            }
        };

        return controller;
    }

    [Fact]
    public async Task Login_with_valid_credentials_returns_200_with_tokens_and_user_summary()
    {
        // Arrange
        var controller = CreateController();
        _authService.AuthenticateAsync(Arg.Any<LoginCommand>(), Arg.Any<CancellationToken>())
            .Returns(new AuthOutcome(LoginStatus.Succeeded, "user-1"));

        var userDetail = new UserDetail(
            "user-1", "Alice Admin", "alice", "alice@example.com", Roles.Admin,
            true, false, null, null, null, false, null, FixedNowUtc, 0, 0, 0);

        _userAdminService.GetAsync("user-1", Arg.Any<CancellationToken>())
            .Returns(Result<UserDetail>.Ok(userDetail));

        _tokenService.GenerateAccessToken("user-1", "alice", "alice@example.com", Roles.Admin)
            .Returns(new GeneratedToken("access-token-xyz", 3600, "Bearer"));

        var request = new LoginRequest("alice", "Password123!");

        // Act
        var result = await controller.Login(request, CancellationToken.None);

        // Assert
        var okResult = result.ShouldBeOfType<OkObjectResult>();
        var dto = okResult.Value.ShouldBeOfType<AuthTokensDto>();

        dto.AccessToken.ShouldBe("access-token-xyz");
        dto.ExpiresInSeconds.ShouldBe(3600);
        dto.TokenType.ShouldBe("Bearer");
        dto.User.ShouldNotBeNull();
        dto.User.Id.ShouldBe("user-1");
        dto.User.FullName.ShouldBe("Alice Admin");
        dto.User.UserName.ShouldBe("alice");
        dto.User.Email.ShouldBe("alice@example.com");
        dto.User.Role.ShouldBe(Roles.Admin);
        dto.User.Permissions.ShouldContain(Permissions.ApiAccess);
    }

    [Fact]
    public async Task Login_with_invalid_credentials_returns_401_generic_problem()
    {
        // Arrange
        var controller = CreateController();
        _authService.AuthenticateAsync(Arg.Any<LoginCommand>(), Arg.Any<CancellationToken>())
            .Returns(new AuthOutcome(LoginStatus.InvalidCredentials));

        var request = new LoginRequest("alice", "WrongPassword!");

        // Act
        var result = await controller.Login(request, CancellationToken.None);

        // Assert
        var unauthorizedResult = result.ShouldBeOfType<UnauthorizedObjectResult>();
        var problem = unauthorizedResult.Value.ShouldBeOfType<ProblemDetails>();
        problem.Status.ShouldBe(StatusCodes.Status401Unauthorized);
        problem.Detail.ShouldBe("Invalid username or password.");
    }

    [Fact]
    public async Task Login_with_locked_account_returns_423_with_retryAfterSeconds()
    {
        // Arrange
        var controller = CreateController();
        var lockEnd = new DateTime(2026, 10, 20, 12, 15, 0, DateTimeKind.Utc);
        _clock.UtcNow.Returns(FixedNowUtc);

        _authService.AuthenticateAsync(Arg.Any<LoginCommand>(), Arg.Any<CancellationToken>())
            .Returns(new AuthOutcome(LoginStatus.LockedOut, LockoutEndUtc: lockEnd));

        var request = new LoginRequest("alice", "Password123!");

        // Act
        var result = await controller.Login(request, CancellationToken.None);

        // Assert
        var objResult = result.ShouldBeOfType<ObjectResult>();
        objResult.StatusCode.ShouldBe(StatusCodes.Status423Locked);

        var problem = objResult.Value.ShouldBeOfType<ProblemDetails>();
        problem.Extensions.ShouldContainKey("retryAfterSeconds");
        problem.Extensions["retryAfterSeconds"].ShouldBe(900);
    }

    [Fact]
    public async Task Login_with_must_change_password_returns_403_problem()
    {
        // Arrange
        var controller = CreateController();
        _authService.AuthenticateAsync(Arg.Any<LoginCommand>(), Arg.Any<CancellationToken>())
            .Returns(new AuthOutcome(LoginStatus.MustChangePassword, "user-1"));

        var request = new LoginRequest("alice", "Password123!");

        // Act
        var result = await controller.Login(request, CancellationToken.None);

        // Assert
        var objResult = result.ShouldBeOfType<ObjectResult>();
        objResult.StatusCode.ShouldBe(StatusCodes.Status403Forbidden);

        var problem = objResult.Value.ShouldBeOfType<ProblemDetails>();
        problem.Type.ShouldBe("https://acxiomcrm.local/problems/password-change-required");
    }

    [Fact]
    public async Task Login_with_empty_credentials_returns_400()
    {
        // Arrange
        var controller = CreateController();
        var request = new LoginRequest("", "");

        // Act
        var result = await controller.Login(request, CancellationToken.None);

        // Assert
        var badResult = result.ShouldBeOfType<BadRequestObjectResult>();
        var problem = badResult.Value.ShouldBeOfType<ProblemDetails>();
        problem.Status.ShouldBe(StatusCodes.Status400BadRequest);
    }

    [Fact]
    public async Task Me_returns_current_user_summary_when_authenticated()
    {
        // Arrange
        var controller = CreateController();
        _currentUser.UserId.Returns("user-1");

        var userDetail = new UserDetail(
            "user-1", "Bob Manager", "bob", "bob@example.com", Roles.Manager,
            true, false, null, null, null, false, null, FixedNowUtc, 0, 0, 0);

        _userAdminService.GetAsync("user-1", Arg.Any<CancellationToken>())
            .Returns(Result<UserDetail>.Ok(userDetail));

        // Act
        var result = await controller.Me(CancellationToken.None);

        // Assert
        var okResult = result.ShouldBeOfType<OkObjectResult>();
        var dto = okResult.Value.ShouldBeOfType<CurrentUserDto>();

        dto.Id.ShouldBe("user-1");
        dto.FullName.ShouldBe("Bob Manager");
        dto.UserName.ShouldBe("bob");
        dto.Email.ShouldBe("bob@example.com");
        dto.Role.ShouldBe(Roles.Manager);
        dto.Permissions.ShouldContain(Permissions.CustomersView);
    }

    [Fact]
    public async Task Me_returns_401_when_user_id_is_missing()
    {
        // Arrange
        var controller = CreateController();
        _currentUser.UserId.Returns((string?)null);

        // Act
        var result = await controller.Me(CancellationToken.None);

        // Assert
        var unauthorizedResult = result.ShouldBeOfType<UnauthorizedObjectResult>();
        var problem = unauthorizedResult.Value.ShouldBeOfType<ProblemDetails>();
        problem.Status.ShouldBe(StatusCodes.Status401Unauthorized);
    }
}

using AcxiomCrm.Application.Abstractions;
using AcxiomCrm.Application.Auth;
using AcxiomCrm.Application.Users;
using AcxiomCrm.Domain.Security;
using AcxiomCrm.Web.Controllers.Api.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AcxiomCrm.Web.Controllers.Api;

[ApiController]
[Route("api/auth")]
[Produces("application/json")]
public class AuthApiController : ControllerBase
{
    private readonly IAuthService _authService;
    private readonly ITokenService _tokenService;
    private readonly IUserAdminService _userAdminService;
    private readonly ICurrentUser _currentUser;
    private readonly IClock _clock;

    public AuthApiController(
        IAuthService authService,
        ITokenService tokenService,
        IUserAdminService userAdminService,
        ICurrentUser currentUser,
        IClock clock)
    {
        _authService = authService;
        _tokenService = tokenService;
        _userAdminService = userAdminService;
        _currentUser = currentUser;
        _clock = clock;
    }

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login([FromBody] LoginRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.UserNameOrEmail) || string.IsNullOrWhiteSpace(request.Password))
        {
            return BadRequest(ProblemFactory.CreateProblem(
                HttpContext,
                StatusCodes.Status400BadRequest,
                "Bad Request",
                "Username/email and password are required."));
        }

        var outcome = await _authService.AuthenticateAsync(
            new LoginCommand(request.UserNameOrEmail, request.Password), ct);

        if (outcome.Status == LoginStatus.LockedOut)
        {
            var retryAfter = outcome.LockoutEndUtc.HasValue
                ? Math.Max(1, (int)(outcome.LockoutEndUtc.Value - _clock.UtcNow).TotalSeconds)
                : 900;

            var extensions = new Dictionary<string, object?>
            {
                ["retryAfterSeconds"] = retryAfter
            };

            return StatusCode(StatusCodes.Status423Locked, ProblemFactory.CreateProblem(
                HttpContext,
                StatusCodes.Status423Locked,
                "Account Locked",
                "Account is locked due to multiple failed login attempts.",
                extensions: extensions));
        }

        if (outcome.Status == LoginStatus.MustChangePassword)
        {
            return StatusCode(StatusCodes.Status403Forbidden, ProblemFactory.CreateProblem(
                HttpContext,
                StatusCodes.Status403Forbidden,
                "Password change required",
                "Password must be changed before accessing the API.",
                type: "https://acxiomcrm.local/problems/password-change-required"));
        }

        if (outcome.Status != LoginStatus.Succeeded || string.IsNullOrEmpty(outcome.UserId))
        {
            return Unauthorized(ProblemFactory.CreateProblem(
                HttpContext,
                StatusCodes.Status401Unauthorized,
                "Unauthorized",
                "Invalid username or password."));
        }

        var userResult = await _userAdminService.GetAsync(outcome.UserId, ct);
        if (!userResult.Succeeded || userResult.Value == null)
        {
            return Unauthorized(ProblemFactory.CreateProblem(
                HttpContext,
                StatusCodes.Status401Unauthorized,
                "Unauthorized",
                "Invalid username or password."));
        }

        var user = userResult.Value;
        var token = _tokenService.GenerateAccessToken(user.Id, user.UserName, user.Email, user.Role);
        var permissions = RolePermissionMap.For(user.Role).ToArray();

        var currentUserDto = new CurrentUserDto(
            user.Id,
            user.FullName,
            user.UserName,
            user.Email,
            user.Role,
            permissions);

        var response = new AuthTokensDto(
            token.AccessToken,
            token.ExpiresInSeconds,
            null,
            token.TokenType,
            currentUserDto);

        return Ok(response);
    }

    [HttpGet("me")]
    [Authorize]
    public async Task<IActionResult> Me(CancellationToken ct)
    {
        var userId = _currentUser.UserId;
        if (string.IsNullOrEmpty(userId))
        {
            return Unauthorized(ProblemFactory.CreateProblem(
                HttpContext,
                StatusCodes.Status401Unauthorized,
                "Unauthorized",
                "User is not authenticated."));
        }

        var userResult = await _userAdminService.GetAsync(userId, ct);
        if (!userResult.Succeeded || userResult.Value == null)
        {
            return NotFound(ProblemFactory.CreateProblem(
                HttpContext,
                StatusCodes.Status404NotFound,
                "Not Found",
                "User not found."));
        }

        var user = userResult.Value;
        var permissions = RolePermissionMap.For(user.Role).ToArray();

        var dto = new CurrentUserDto(
            user.Id,
            user.FullName,
            user.UserName,
            user.Email,
            user.Role,
            permissions);

        return Ok(dto);
    }
}

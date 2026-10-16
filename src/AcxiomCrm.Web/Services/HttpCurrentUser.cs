using System.Security.Claims;
using AcxiomCrm.Application.Abstractions;

namespace AcxiomCrm.Web.Services;

public sealed class HttpCurrentUser(IHttpContextAccessor httpContextAccessor) : ICurrentUser
{
    private HttpContext? Context => httpContextAccessor.HttpContext;

    public string? UserId => Context?.User.FindFirstValue(ClaimTypes.NameIdentifier);

    public string? UserName => Context?.User.Identity?.Name ?? Context?.User.FindFirstValue(ClaimTypes.Name);

    public string? Role => Context?.User.FindFirstValue(ClaimTypes.Role);

    public bool IsAuthenticated => Context?.User.Identity?.IsAuthenticated ?? false;

    public string? IpAddress => Context?.Connection.RemoteIpAddress?.ToString();

    public string? CorrelationId
    {
        get
        {
            if (Context == null)
            {
                return null;
            }

            if (Context.Items.TryGetValue("CorrelationId", out var id) && id != null)
            {
                return id.ToString();
            }

            var newId = Guid.NewGuid().ToString("N");
            Context.Items["CorrelationId"] = newId;
            return newId;
        }
    }
}

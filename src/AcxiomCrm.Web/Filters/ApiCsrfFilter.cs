using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace AcxiomCrm.Web.Filters;

public sealed class ApiCsrfFilter(IAntiforgery antiforgery) : IAsyncAuthorizationFilter
{
    private static readonly HashSet<string> UnsafeMethods = new(StringComparer.OrdinalIgnoreCase)
    {
        HttpMethods.Post,
        HttpMethods.Put,
        HttpMethods.Delete,
        HttpMethods.Patch
    };

    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        if (!UnsafeMethods.Contains(context.HttpContext.Request.Method))
        {
            return;
        }

        // Only enforce CSRF if authenticated via Cookie (Identity ApplicationScheme)
        var isCookieAuth = context.HttpContext.User.Identities.Any(i =>
            i.IsAuthenticated &&
            (string.Equals(i.AuthenticationType, IdentityConstants.ApplicationScheme, StringComparison.OrdinalIgnoreCase) ||
             string.Equals(i.AuthenticationType, "Identity.Application", StringComparison.OrdinalIgnoreCase)));

        if (isCookieAuth)
        {
            var isValid = await antiforgery.IsRequestValidAsync(context.HttpContext);
            if (!isValid)
            {
                context.Result = new BadRequestObjectResult(new
                {
                    type = "https://acxiomcrm.local/problems/csrf",
                    title = "Invalid Anti-Forgery Token",
                    status = 400,
                    detail = "Cookie-authenticated requests require a valid X-CSRF-TOKEN header.",
                    traceId = context.HttpContext.TraceIdentifier
                });
            }
        }
    }
}

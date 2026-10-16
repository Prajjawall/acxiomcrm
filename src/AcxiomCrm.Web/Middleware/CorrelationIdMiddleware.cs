using Serilog.Context;

namespace AcxiomCrm.Web.Middleware;

public class CorrelationIdMiddleware
{
    public const string HeaderName = "X-Correlation-Id";
    private readonly RequestDelegate _next;

    public CorrelationIdMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = GetOrGenerateCorrelationId(context);

        context.Response.OnStarting(() =>
        {
            context.Response.Headers[HeaderName] = correlationId;
            return Task.CompletedTask;
        });

        using (LogContext.PushProperty("CorrelationId", correlationId))
        {
            await _next(context);
        }
    }

    private static string GetOrGenerateCorrelationId(HttpContext context)
    {
        if (context.Request.Headers.TryGetValue(HeaderName, out var values))
        {
            var candidate = values.FirstOrDefault();
            if (IsValidCorrelationId(candidate))
            {
                return candidate!;
            }
        }

        return Guid.NewGuid().ToString("N");
    }

    private static bool IsValidCorrelationId(string? candidate)
    {
        if (string.IsNullOrWhiteSpace(candidate))
        {
            return false;
        }

        if (candidate.Length > 64)
        {
            return false;
        }

        foreach (var c in candidate)
        {
            if (char.IsControl(c))
            {
                return false;
            }
        }

        return true;
    }
}

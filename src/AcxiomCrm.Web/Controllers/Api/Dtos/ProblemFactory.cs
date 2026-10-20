using AcxiomCrm.Domain.Validation;
using Microsoft.AspNetCore.Mvc;

namespace AcxiomCrm.Web.Controllers.Api.Dtos;

public static class ProblemFactory
{
    public static ValidationProblemDetails CreateValidationProblem(
        HttpContext httpContext,
        IReadOnlyList<FieldError> errors)
    {
        var errorDict = new Dictionary<string, string[]>(StringComparer.Ordinal);
        foreach (var group in errors.GroupBy(e => ToCamelCase(e.Field)))
        {
            errorDict[group.Key] = group.Select(e => e.Message).ToArray();
        }

        var problem = new ValidationProblemDetails(errorDict)
        {
            Type = "https://acxiomcrm.local/problems/validation",
            Title = "One or more validation errors occurred.",
            Status = StatusCodes.Status400BadRequest,
            Instance = httpContext.Request.Path
        };
        problem.Extensions["traceId"] = httpContext.TraceIdentifier;
        return problem;
    }

    public static ProblemDetails CreateProblem(
        HttpContext httpContext,
        int statusCode,
        string title,
        string? detail = null,
        string? type = null,
        IDictionary<string, object?>? extensions = null)
    {
        var problem = new ProblemDetails
        {
            Type = type ?? $"https://acxiomcrm.local/problems/{statusCode}",
            Title = title,
            Status = statusCode,
            Detail = detail,
            Instance = httpContext.Request.Path
        };
        problem.Extensions["traceId"] = httpContext.TraceIdentifier;
        if (extensions != null)
        {
            foreach (var kvp in extensions)
            {
                problem.Extensions[kvp.Key] = kvp.Value;
            }
        }
        return problem;
    }

    public static string ToCamelCase(string? str)
    {
        if (string.IsNullOrWhiteSpace(str))
        {
            return "general";
        }
        if (char.IsLower(str[0]))
        {
            return str;
        }
        return char.ToLowerInvariant(str[0]) + str[1..];
    }
}

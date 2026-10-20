using AcxiomCrm.Application.Common;
using AcxiomCrm.Domain.Security;
using AcxiomCrm.Web.Controllers.Api.Dtos;
using AcxiomCrm.Web.Filters;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AcxiomCrm.Web.Controllers.Api;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
[Authorize(Policy = Permissions.ApiAccess)]
[IgnoreAntiforgeryToken]
[ServiceFilter(typeof(ApiCsrfFilter))]
public abstract class ApiControllerBase : ControllerBase
{
    protected IActionResult ToActionResult(Result result)
    {
        if (result.Succeeded)
        {
            return NoContent();
        }

        return MapFailure(result.Kind, result.Errors);
    }

    protected IActionResult ToActionResult<T>(
        Result<T> result,
        Func<T, object>? map = null,
        Func<T, string>? location = null)
    {
        if (result.Succeeded)
        {
            var val = result.Value!;
            var payload = map != null ? map(val) : val;

            if (location != null)
            {
                return Created(location(val), payload);
            }

            return Ok(payload);
        }

        return MapFailure(result.Kind, result.Errors);
    }

    private ObjectResult MapFailure(ErrorKind kind, IReadOnlyList<AcxiomCrm.Domain.Validation.FieldError> errors)
    {
        return kind switch
        {
            ErrorKind.Validation => BadRequest(ProblemFactory.CreateValidationProblem(HttpContext, errors)),
            ErrorKind.NotFound => NotFound(ProblemFactory.CreateProblem(HttpContext, StatusCodes.Status404NotFound, "Not Found", "Resource was not found or is out of scope.")),
            ErrorKind.Forbidden => StatusCode(StatusCodes.Status403Forbidden, ProblemFactory.CreateProblem(HttpContext, StatusCodes.Status403Forbidden, "Forbidden", "You do not have permission to access this resource.")),
            ErrorKind.Conflict => Conflict(ProblemFactory.CreateProblem(HttpContext, StatusCodes.Status409Conflict, "Conflict", errors.Count > 0 ? errors[0].Message : "A business conflict occurred.")),
            _ => StatusCode(StatusCodes.Status500InternalServerError, ProblemFactory.CreateProblem(HttpContext, StatusCodes.Status500InternalServerError, "An unexpected error occurred."))
        };
    }
}

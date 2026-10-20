using AcxiomCrm.Domain.Security;
using AcxiomCrm.Web.Controllers.Api.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AcxiomCrm.Web.Controllers.Api;

[Authorize(Policy = Permissions.ReportsPipeline)]
public class ReportsApiController : ApiControllerBase
{
    [HttpGet("pipeline")]
    public IActionResult GetPipeline(
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        [FromQuery] string? ownerId)
    {
        // Per Plan 10: "Until plan 11 lands, this action returns 501 not-implemented problem and the test is [Trait(\"until\",\"plan-11\")]; plan 11 flips it."
        return StatusCode(StatusCodes.Status501NotImplemented, ProblemFactory.CreateProblem(
            HttpContext,
            StatusCodes.Status501NotImplemented,
            "Not Implemented",
            "Pipeline report endpoint will be fully implemented in Plan 11."));
    }
}

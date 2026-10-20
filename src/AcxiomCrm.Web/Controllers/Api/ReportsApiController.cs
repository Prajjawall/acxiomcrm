using AcxiomCrm.Application.Reporting;
using AcxiomCrm.Application.Reports;
using AcxiomCrm.Domain.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AcxiomCrm.Web.Controllers.Api;

[Authorize(Policy = Permissions.ReportsPipeline)]
public class ReportsApiController : ApiControllerBase
{
    private readonly IReportService _reportService;

    public ReportsApiController(IReportService reportService)
    {
        _reportService = reportService;
    }

    [HttpGet("pipeline")]
    public async Task<IActionResult> GetPipeline(
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        [FromQuery] string? ownerId,
        CancellationToken ct = default)
    {
        var filter = new ReportFilter(new DateRange(from, to), ownerId);
        var result = await _reportService.GetPipelineAsync(filter, ct);
        return ToActionResult(result);
    }
}

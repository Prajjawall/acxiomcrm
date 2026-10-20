using AcxiomCrm.Application.Common;
using AcxiomCrm.Application.Reporting;
using AcxiomCrm.Application.Reports;
using AcxiomCrm.Domain.Enums;
using AcxiomCrm.Domain.Security;
using AcxiomCrm.Web.ViewModels.Reports;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AcxiomCrm.Web.Controllers;

[Authorize]
public class ReportsController : Controller
{
    private readonly IReportService _reportService;

    public ReportsController(IReportService reportService)
    {
        _reportService = reportService;
    }

    [HttpGet]
    [Authorize(Policy = Permissions.ReportsCustomers)]
    public IActionResult Index()
    {
        return View();
    }

    [HttpGet]
    [Authorize(Policy = Permissions.ReportsCustomers)]
    public async Task<IActionResult> Customers(
        [FromQuery] string? search,
        [FromQuery] CustomerStatus? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        CancellationToken ct = default)
    {
        var filter = new CustomerReportFilter(search, status);
        var pageQuery = new PageQuery(page, pageSize);
        var result = await _reportService.RunCustomerReportAsync(filter, pageQuery, ct);

        var vm = new CustomerReportViewModel
        {
            Filter = filter,
            Result = result,
            Search = search,
            Status = status
        };

        return View(vm);
    }

    [HttpGet]
    [Authorize(Policy = Permissions.ReportsPipeline)]
    public async Task<IActionResult> Pipeline(
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        CancellationToken ct = default)
    {
        var filter = new ReportFilter(new DateRange(from, to));
        var result = await _reportService.GetPipelineAsync(filter, ct);

        var vm = new PipelineReportViewModel
        {
            Report = result.Value ?? new PipelineReportDto([], [], new(0, 0, 0)),
            From = from,
            To = to
        };

        return View(vm);
    }

    [HttpGet]
    [Authorize(Policy = Permissions.ReportsFollowUps)]
    public async Task<IActionResult> FollowUps(
        [FromQuery] FollowUpStatus? status,
        [FromQuery] bool? overdue,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        CancellationToken ct = default)
    {
        var filter = new FollowUpReportFilter(status, overdue);
        var pageQuery = new PageQuery(page, pageSize);
        var result = await _reportService.RunFollowUpReportAsync(filter, pageQuery, ct);

        var vm = new FollowUpReportViewModel
        {
            Filter = filter,
            Result = result,
            Status = status,
            Overdue = overdue
        };

        return View(vm);
    }
}

using AcxiomCrm.Application.Abstractions;
using AcxiomCrm.Application.Dashboard;
using AcxiomCrm.Application.Reporting;
using AcxiomCrm.Domain.Security;
using AcxiomCrm.Web.Services;
using AcxiomCrm.Web.ViewModels.Dashboard;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AcxiomCrm.Web.Controllers;

[Authorize(Policy = Permissions.DashboardView)]
public class DashboardController : Controller
{
    private readonly IDashboardService _dashboardService;
    private readonly ICurrentUser _currentUser;
    private readonly IFlash _flash;

    public DashboardController(
        IDashboardService dashboardService,
        ICurrentUser currentUser,
        IFlash flash)
    {
        _dashboardService = dashboardService;
        _currentUser = currentUser;
        _flash = flash;
    }

    [HttpGet]
    public async Task<IActionResult> Index(
        [FromQuery] DatePreset preset = DatePreset.Month,
        [FromQuery] DateOnly? from = null,
        [FromQuery] DateOnly? to = null,
        CancellationToken ct = default)
    {
        var result = await _dashboardService.GetAsync(preset, from, to, ct);
        if (!result.Succeeded)
        {
            var firstError = result.Errors.Count > 0 ? result.Errors[0].Message : "Invalid date filter.";
            _flash.Error(firstError);
            preset = DatePreset.Month;
            from = null;
            to = null;
            result = await _dashboardService.GetAsync(preset, null, null, ct);
        }

        var data = result.Value!;
        var vm = new DashboardViewModel
        {
            Preset = preset,
            From = from,
            To = to,
            Cards = data.Cards,
            LeadStatus = data.LeadStatus,
            Pipeline = data.Pipeline,
            MonthlySales = data.MonthlySales,
            WeightedPipeline = data.WeightedPipeline,
            Panels = data.Panels,
            UserRole = _currentUser.Role ?? string.Empty
        };

        return View(vm);
    }

    [HttpGet("Dashboard/Data")]
    public async Task<IActionResult> Data(
        [FromQuery] DatePreset preset = DatePreset.Month,
        [FromQuery] DateOnly? from = null,
        [FromQuery] DateOnly? to = null,
        CancellationToken ct = default)
    {
        var result = await _dashboardService.GetAsync(preset, from, to, ct);
        if (!result.Succeeded)
        {
            return BadRequest(new { error = result.Errors.Count > 0 ? result.Errors[0].Message : "Invalid date filter." });
        }

        var data = result.Value!;
        return Json(new
        {
            leadStatus = data.LeadStatus,
            pipeline = data.Pipeline,
            monthlySales = data.MonthlySales,
            weightedPipeline = data.WeightedPipeline
        });
    }
}

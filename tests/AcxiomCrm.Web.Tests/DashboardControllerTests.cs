using AcxiomCrm.Application.Abstractions;
using AcxiomCrm.Application.Common;
using AcxiomCrm.Application.Dashboard;
using AcxiomCrm.Application.Reporting;
using AcxiomCrm.Domain.Security;
using AcxiomCrm.Domain.Validation;
using AcxiomCrm.Web.Controllers;
using AcxiomCrm.Web.Services;
using AcxiomCrm.Web.ViewModels.Dashboard;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using Shouldly;

namespace AcxiomCrm.Web.Tests;

public class DashboardControllerTests
{
    private readonly IDashboardService _dashboardService = Substitute.For<IDashboardService>();
    private readonly ICurrentUser _currentUser = Substitute.For<ICurrentUser>();
    private readonly IFlash _flash = Substitute.For<IFlash>();

    private DashboardController CreateController(string role = Roles.Admin)
    {
        _currentUser.Role.Returns(role);
        return new DashboardController(_dashboardService, _currentUser, _flash)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };
    }

    [Fact]
    public async Task Index_returns_view_with_dashboard_data()
    {
        // Arrange
        var controller = CreateController();
        var cards = new List<KpiCard>
        {
            new("totalCustomers", "Total Customers", 10, false, null, "Hint")
        };
        var dashboardData = new DashboardData(
            new DateRange(null, null),
            cards,
            [],
            [],
            [],
            150000m,
            new RolePanels(null, null, null));

        _dashboardService.GetAsync(DatePreset.Month, null, null, Arg.Any<CancellationToken>())
            .Returns(Result<DashboardData>.Ok(dashboardData));

        // Act
        var result = await controller.Index(DatePreset.Month, null, null, CancellationToken.None);

        // Assert
        var viewResult = result.ShouldBeOfType<ViewResult>();
        var vm = viewResult.Model.ShouldBeOfType<DashboardViewModel>();
        vm.Cards.Count.ShouldBe(1);
        vm.WeightedPipeline.ShouldBe(150000m);
        vm.UserRole.ShouldBe(Roles.Admin);
    }

    [Fact]
    public async Task Index_with_invalid_date_range_flashes_error_and_defaults_to_month()
    {
        // Arrange
        var controller = CreateController();
        var from = new DateOnly(2026, 10, 20);
        var to = new DateOnly(2026, 10, 10);

        _dashboardService.GetAsync(DatePreset.Custom, from, to, Arg.Any<CancellationToken>())
            .Returns(Result<DashboardData>.Fail(ErrorKind.Validation, new FieldError("From", "Start date must not be after end date.")));

        var fallbackData = new DashboardData(
            new DateRange(null, null),
            [],
            [],
            [],
            [],
            0m,
            new RolePanels(null, null, null));

        _dashboardService.GetAsync(DatePreset.Month, null, null, Arg.Any<CancellationToken>())
            .Returns(Result<DashboardData>.Ok(fallbackData));

        // Act
        var result = await controller.Index(DatePreset.Custom, from, to, CancellationToken.None);

        // Assert
        _flash.Received(1).Error("Start date must not be after end date.");
        var viewResult = result.ShouldBeOfType<ViewResult>();
        var vm = viewResult.Model.ShouldBeOfType<DashboardViewModel>();
        vm.Preset.ShouldBe(DatePreset.Month);
    }

    [Fact]
    public async Task Data_returns_json_result()
    {
        // Arrange
        var controller = CreateController();
        var dashboardData = new DashboardData(
            new DateRange(null, null),
            [],
            [new LabelValue("New", 5)],
            [new StageSeries("Proposal", 2, 20000m, 10000m)],
            [],
            10000m,
            new RolePanels(null, null, null));

        _dashboardService.GetAsync(DatePreset.Month, null, null, Arg.Any<CancellationToken>())
            .Returns(Result<DashboardData>.Ok(dashboardData));

        // Act
        var result = await controller.Data(DatePreset.Month, null, null, CancellationToken.None);

        // Assert
        var jsonResult = result.ShouldBeOfType<JsonResult>();
        jsonResult.Value.ShouldNotBeNull();
    }
}

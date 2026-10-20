using AcxiomCrm.Application.Common;
using AcxiomCrm.Application.Reporting;
using AcxiomCrm.Application.Reports;
using AcxiomCrm.Domain.Enums;
using AcxiomCrm.Web.Controllers;
using AcxiomCrm.Web.ViewModels.Reports;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using Shouldly;

namespace AcxiomCrm.Web.Tests;

public class ReportsControllerTests
{
    private static readonly DateTime FixedNowUtc = new(2026, 10, 20, 12, 0, 0, DateTimeKind.Utc);
    private readonly IReportService _reportService = Substitute.For<IReportService>();

    private ReportsController CreateController()
    {
        return new ReportsController(_reportService)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };
    }

    [Fact]
    public void Index_returns_view()
    {
        var controller = CreateController();
        var result = controller.Index();
        result.ShouldBeOfType<ViewResult>();
    }

    [Fact]
    public async Task Customers_returns_view_with_customer_report_model()
    {
        var controller = CreateController();
        var items = new List<CustomerReportItem>
        {
            new(1, "CUST-0001", "Acme Corp", "Acme", "acme@example.com", "9876543210", CustomerStatus.Active, "Alice", FixedNowUtc)
        };
        _reportService.RunCustomerReportAsync(Arg.Any<CustomerReportFilter>(), Arg.Any<PageQuery>(), Arg.Any<CancellationToken>())
            .Returns(new PagedResult<CustomerReportItem>(items, 1, 25, 1));

        var result = await controller.Customers(null, null, 1, 25, CancellationToken.None);

        var viewResult = result.ShouldBeOfType<ViewResult>();
        var vm = viewResult.Model.ShouldBeOfType<CustomerReportViewModel>();
        vm.Result.Items.Count.ShouldBe(1);
    }

    [Fact]
    public async Task Pipeline_returns_view_with_pipeline_report_model()
    {
        var controller = CreateController();
        var dto = new PipelineReportDto(
            [new PipelineStageSummary(OpportunityStage.Proposal, 1, 10000m, 5000m)],
            [new PipelineOwnerSummary("user-1", "Alice", 1, 10000m, 5000m)],
            new PipelineTotals(1, 10000m, 5000m));

        _reportService.GetPipelineAsync(Arg.Any<ReportFilter>(), Arg.Any<CancellationToken>())
            .Returns(Result<PipelineReportDto>.Ok(dto));

        var result = await controller.Pipeline(null, null, CancellationToken.None);

        var viewResult = result.ShouldBeOfType<ViewResult>();
        var vm = viewResult.Model.ShouldBeOfType<PipelineReportViewModel>();
        vm.Report.Totals.TotalAmount.ShouldBe(10000m);
    }

    [Fact]
    public async Task FollowUps_returns_view_with_followup_report_model()
    {
        var controller = CreateController();
        var items = new List<FollowUpReportItem>
        {
            new(1, RelatedType.Customer, "Acme Corp", "Intro call", FollowUpType.Call, FollowUpStatus.Planned, FixedNowUtc, "Alice", false)
        };
        _reportService.RunFollowUpReportAsync(Arg.Any<FollowUpReportFilter>(), Arg.Any<PageQuery>(), Arg.Any<CancellationToken>())
            .Returns(new PagedResult<FollowUpReportItem>(items, 1, 25, 1));

        var result = await controller.FollowUps(null, null, 1, 25, CancellationToken.None);

        var viewResult = result.ShouldBeOfType<ViewResult>();
        var vm = viewResult.Model.ShouldBeOfType<FollowUpReportViewModel>();
        vm.Result.Items.Count.ShouldBe(1);
    }
}

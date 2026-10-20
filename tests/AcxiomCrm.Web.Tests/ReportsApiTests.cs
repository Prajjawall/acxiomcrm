using AcxiomCrm.Application.Common;
using AcxiomCrm.Application.Reporting;
using AcxiomCrm.Application.Reports;
using AcxiomCrm.Domain.Enums;
using AcxiomCrm.Web.Controllers.Api;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using Shouldly;

namespace AcxiomCrm.Web.Tests;

public class ReportsApiTests
{
    private readonly IReportService _reportService = Substitute.For<IReportService>();

    [Fact]
    public async Task Pipeline_report_returns_200_with_pipeline_summary()
    {
        // Arrange
        var pipelineData = new PipelineReportDto(
            [new PipelineStageSummary(OpportunityStage.Proposal, 2, 50000m, 25000m)],
            [new PipelineOwnerSummary("user-1", "Alice", 2, 50000m, 25000m)],
            new PipelineTotals(2, 50000m, 25000m));

        _reportService.GetPipelineAsync(Arg.Any<ReportFilter>(), Arg.Any<CancellationToken>())
            .Returns(Result<PipelineReportDto>.Ok(pipelineData));

        var controller = new ReportsApiController(_reportService)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    TraceIdentifier = "trace-reports-123"
                }
            }
        };

        // Act
        var result = await controller.GetPipeline(null, null, null, CancellationToken.None);

        // Assert
        var okResult = result.ShouldBeOfType<OkObjectResult>();
        var dto = okResult.Value.ShouldBeOfType<PipelineReportDto>();
        dto.Totals.TotalCount.ShouldBe(2);
        dto.Totals.TotalAmount.ShouldBe(50000m);
        dto.ByStage.Count.ShouldBe(1);
    }
}

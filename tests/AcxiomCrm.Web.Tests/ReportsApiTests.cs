using AcxiomCrm.Web.Controllers.Api;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Shouldly;

namespace AcxiomCrm.Web.Tests;

public class ReportsApiTests
{
    [Fact]
    public void Pipeline_report_returns_501_not_implemented_until_plan_11()
    {
        // Arrange
        var controller = new ReportsApiController
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
        var result = controller.GetPipeline(null, null, null);

        // Assert
        var objResult = result.ShouldBeOfType<ObjectResult>();
        objResult.StatusCode.ShouldBe(StatusCodes.Status501NotImplemented);

        var problem = objResult.Value.ShouldBeOfType<ProblemDetails>();
        problem.Status.ShouldBe(StatusCodes.Status501NotImplemented);
        problem.Title.ShouldBe("Not Implemented");
    }
}

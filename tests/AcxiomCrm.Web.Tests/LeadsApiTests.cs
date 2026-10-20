using AcxiomCrm.Application.Abstractions;
using AcxiomCrm.Application.Common;
using AcxiomCrm.Application.Leads;
using AcxiomCrm.Domain.Enums;
using AcxiomCrm.Domain.Security;
using AcxiomCrm.Web.Controllers.Api;
using AcxiomCrm.Web.Controllers.Api.Dtos;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using Shouldly;

namespace AcxiomCrm.Web.Tests;

public class LeadsApiTests
{
    private static readonly DateTime FixedNowUtc = new(2026, 10, 20, 12, 0, 0, DateTimeKind.Utc);
    private readonly ILeadService _leadService = Substitute.For<ILeadService>();
    private readonly ICurrentUser _currentUser = Substitute.For<ICurrentUser>();

    private LeadsApiController CreateController()
    {
        _currentUser.Role.Returns(Roles.Admin);
        _currentUser.UserId.Returns("user-1");

        var httpContext = new DefaultHttpContext();
        httpContext.TraceIdentifier = "trace-leads-123";

        return new LeadsApiController(_leadService, _currentUser)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = httpContext
            }
        };
    }

    [Fact]
    public async Task List_returns_paged_leads()
    {
        // Arrange
        var controller = CreateController();
        var items = new List<LeadListItem>
        {
            new(1, "LEAD-0001", "John Doe", "Doe Enterprises", "john@example.com", "9876543210",
                LeadSource.Website, LeadStatus.New, LeadPriority.High, 50000m, "user-1", "Alice", FixedNowUtc)
        };

        _leadService.SearchAsync(Arg.Any<LeadFilter>(), Arg.Any<PageQuery>(), Arg.Any<CancellationToken>())
            .Returns(new PagedResult<LeadListItem>(items, 1, 25, 1));

        // Act
        var result = await controller.List(null, null, null, null, null, 1, 25, null, false, CancellationToken.None);

        // Assert
        var okResult = result.ShouldBeOfType<OkObjectResult>();
        var paged = okResult.Value.ShouldBeOfType<PagedDto<LeadDto>>();
        paged.Items.Count.ShouldBe(1);
        paged.Items[0].Name.ShouldBe("John Doe");
        paged.Items[0].Code.ShouldBe("LEAD-0001");
    }

    [Fact]
    public async Task Get_by_id_returns_lead_when_found()
    {
        // Arrange
        var controller = CreateController();
        var detail = new LeadDetail(
            1, "LEAD-0001", "John Doe", null, "john@example.com", "9876543210",
            LeadSource.Referral, LeadStatus.Contacted, LeadPriority.Medium, 25000m,
            "user-1", "Alice", "Notes", null, null, null, null,
            FixedNowUtc, null, [1], [LeadStatus.Qualified, LeadStatus.Lost]);

        _leadService.GetAsync(1, Arg.Any<CancellationToken>())
            .Returns(Result<LeadDetail>.Ok(detail));

        // Act
        var result = await controller.Get(1, CancellationToken.None);

        // Assert
        var okResult = result.ShouldBeOfType<OkObjectResult>();
        var dto = okResult.Value.ShouldBeOfType<LeadDto>();
        dto.Id.ShouldBe(1);
        dto.Name.ShouldBe("John Doe");
        dto.Source.ShouldBe(LeadSource.Referral);
    }

    [Fact]
    public async Task Create_returns_201_with_location_header()
    {
        // Arrange
        var controller = CreateController();
        var detail = new LeadDetail(
            2, "LEAD-0002", "Jane Smith", null, "jane@example.com", "9876543211",
            LeadSource.ColdCall, LeadStatus.New, LeadPriority.Low, 10000m,
            "user-1", "Alice", null, null, null, null, null,
            FixedNowUtc, null, [1], []);

        _leadService.CreateAsync(Arg.Any<CreateLeadCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result<LeadDetail>.Ok(detail));

        var request = new CreateLeadRequest("Jane Smith", null, "jane@example.com", "9876543211", LeadSource.ColdCall, LeadPriority.Low, 10000m, null, null);

        // Act
        var result = await controller.Create(request, CancellationToken.None);

        // Assert
        var createdResult = result.ShouldBeOfType<CreatedResult>();
        createdResult.Location.ShouldBe("/api/leads/2");
        var dto = createdResult.Value.ShouldBeOfType<LeadDto>();
        dto.Id.ShouldBe(2);
    }
}

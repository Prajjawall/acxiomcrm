using AcxiomCrm.Application.Abstractions;
using AcxiomCrm.Application.Common;
using AcxiomCrm.Application.Opportunities;
using AcxiomCrm.Domain.Enums;
using AcxiomCrm.Domain.Security;
using AcxiomCrm.Web.Controllers.Api;
using AcxiomCrm.Web.Controllers.Api.Dtos;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using Shouldly;

namespace AcxiomCrm.Web.Tests;

public class OpportunitiesApiTests
{
    private static readonly DateTime FixedNowUtc = new(2026, 10, 20, 12, 0, 0, DateTimeKind.Utc);
    private readonly IOpportunityService _opportunityService = Substitute.For<IOpportunityService>();
    private readonly ICurrentUser _currentUser = Substitute.For<ICurrentUser>();

    private OpportunitiesApiController CreateController()
    {
        _currentUser.Role.Returns(Roles.Admin);
        _currentUser.UserId.Returns("user-1");

        var httpContext = new DefaultHttpContext();
        httpContext.TraceIdentifier = "trace-opp-123";

        return new OpportunitiesApiController(_opportunityService, _currentUser)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = httpContext
            }
        };
    }

    [Fact]
    public async Task List_returns_paged_opportunities()
    {
        // Arrange
        var controller = CreateController();
        var items = new List<OpportunityListItem>
        {
            new(1, "Big Deal", 5, "Acme Corp", "CUST-0001", 100000m, 50, 50000m,
                OpportunityStage.Proposal, OpportunityStatus.Open, new DateOnly(2026, 11, 1), false, "user-1", "Alice")
        };

        _opportunityService.SearchAsync(Arg.Any<OpportunityFilter>(), Arg.Any<PageQuery>(), Arg.Any<CancellationToken>())
            .Returns(new PagedResult<OpportunityListItem>(items, 1, 25, 1));

        // Act
        var result = await controller.List(null, null, null, null, null, 1, 25, null, false, CancellationToken.None);

        // Assert
        var okResult = result.ShouldBeOfType<OkObjectResult>();
        var paged = okResult.Value.ShouldBeOfType<PagedDto<OpportunityDto>>();
        paged.Items.Count.ShouldBe(1);
        paged.Items[0].Name.ShouldBe("Big Deal");
        paged.Items[0].Amount.ShouldBe(100000m);
        paged.Items[0].Customer.Name.ShouldBe("Acme Corp");
    }

    [Fact]
    public async Task Get_by_id_returns_opportunity_when_found()
    {
        // Arrange
        var controller = CreateController();
        var detail = new OpportunityDetail(
            1, "Big Deal", 5, "Acme Corp", "CUST-0001", null, 100000m, 50, 50000m,
            OpportunityStage.Proposal, OpportunityStatus.Open, new DateOnly(2026, 11, 1), null, null,
            LeadSource.Website, "user-1", "Alice", "Notes", FixedNowUtc, null, [1]);

        _opportunityService.GetAsync(1, Arg.Any<CancellationToken>())
            .Returns(Result<OpportunityDetail>.Ok(detail));

        // Act
        var result = await controller.Get(1, CancellationToken.None);

        // Assert
        var okResult = result.ShouldBeOfType<OkObjectResult>();
        var dto = okResult.Value.ShouldBeOfType<OpportunityDto>();
        dto.Id.ShouldBe(1);
        dto.Name.ShouldBe("Big Deal");
        dto.Stage.ShouldBe(OpportunityStage.Proposal);
    }

    [Fact]
    public async Task Create_returns_201_with_location_header()
    {
        // Arrange
        var controller = CreateController();
        var detail = new OpportunityDetail(
            2, "New Opportunity", 5, "Acme Corp", "CUST-0001", null, 25000m, 20, 5000m,
            OpportunityStage.Qualification, OpportunityStatus.Open, new DateOnly(2026, 12, 1), null, null,
            null, "user-1", "Alice", null, FixedNowUtc, null, [1]);

        _opportunityService.CreateAsync(Arg.Any<CreateOpportunityCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result<OpportunityDetail>.Ok(detail));

        var request = new CreateOpportunityRequest(
            "New Opportunity", 5, 25000m, 20, new DateOnly(2026, 12, 1), OpportunityStage.Qualification, null, null, null);

        // Act
        var result = await controller.Create(request, CancellationToken.None);

        // Assert
        var createdResult = result.ShouldBeOfType<CreatedResult>();
        createdResult.Location.ShouldBe("/api/opportunities/2");
        var dto = createdResult.Value.ShouldBeOfType<OpportunityDto>();
        dto.Id.ShouldBe(2);
        dto.Amount.ShouldBe(25000m);
    }
}

using AcxiomCrm.Application.Abstractions;
using AcxiomCrm.Application.Common;
using AcxiomCrm.Application.FollowUps;
using AcxiomCrm.Domain.Enums;
using AcxiomCrm.Domain.Security;
using AcxiomCrm.Web.Controllers.Api;
using AcxiomCrm.Web.Controllers.Api.Dtos;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using Shouldly;

namespace AcxiomCrm.Web.Tests;

public class FollowUpsApiTests
{
    private static readonly DateTime FixedNowUtc = new(2026, 10, 20, 12, 0, 0, DateTimeKind.Utc);
    private readonly IFollowUpService _followUpService = Substitute.For<IFollowUpService>();
    private readonly ICurrentUser _currentUser = Substitute.For<ICurrentUser>();

    private FollowUpsApiController CreateController()
    {
        _currentUser.Role.Returns(Roles.Admin);
        _currentUser.UserId.Returns("user-1");

        var httpContext = new DefaultHttpContext();
        httpContext.TraceIdentifier = "trace-fu-123";

        return new FollowUpsApiController(_followUpService, _currentUser)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = httpContext
            }
        };
    }

    [Fact]
    public async Task List_returns_paged_follow_ups()
    {
        // Arrange
        var controller = CreateController();
        var items = new List<FollowUpListItem>
        {
            new(1, RelatedType.Customer, 10, "Customer Beta", false,
                FixedNowUtc.AddDays(1), "Follow up on proposal", FollowUpType.Call,
                FollowUpStatus.Planned, false, false, "user-1", "Alice", 0)
        };

        _followUpService.SearchAsync(Arg.Any<FollowUpFilter>(), Arg.Any<PageQuery>(), Arg.Any<CancellationToken>())
            .Returns(new PagedResult<FollowUpListItem>(items, 1, 25, 1));

        // Act
        var result = await controller.List(null, null, null, null, null, null, null, null, 1, 25, null, false, CancellationToken.None);

        // Assert
        var okResult = result.ShouldBeOfType<OkObjectResult>();
        var paged = okResult.Value.ShouldBeOfType<PagedDto<FollowUpDto>>();
        paged.Items.Count.ShouldBe(1);
        paged.Items[0].Subject.ShouldBe("Follow up on proposal");
        paged.Items[0].RelatedName.ShouldBe("Customer Beta");
    }

    [Fact]
    public async Task Get_by_id_returns_follow_up_when_found()
    {
        // Arrange
        var controller = CreateController();
        var detail = new FollowUpDetail(
            1, RelatedType.Lead, 20, "Lead Gamma", FixedNowUtc.AddDays(2),
            "Intro call", FollowUpType.Call, FollowUpStatus.Planned, "Remarks",
            "user-1", "Alice", null, 0, false, false, FixedNowUtc, [1]);

        _followUpService.GetAsync(1, Arg.Any<CancellationToken>())
            .Returns(Result<FollowUpDetail>.Ok(detail));

        // Act
        var result = await controller.Get(1, CancellationToken.None);

        // Assert
        var okResult = result.ShouldBeOfType<OkObjectResult>();
        var dto = okResult.Value.ShouldBeOfType<FollowUpDto>();
        dto.Id.ShouldBe(1);
        dto.Subject.ShouldBe("Intro call");
        dto.RelatedType.ShouldBe(RelatedType.Lead);
    }

    [Fact]
    public async Task Create_returns_201_with_location_header()
    {
        // Arrange
        var controller = CreateController();
        var detail = new FollowUpDetail(
            2, RelatedType.Customer, 10, "Customer Beta", FixedNowUtc.AddDays(3),
            "Check contract", FollowUpType.Meeting, FollowUpStatus.Planned, null,
            "user-1", "Alice", null, 0, false, false, FixedNowUtc, [1]);

        _followUpService.CreateAsync(Arg.Any<CreateFollowUpCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result<FollowUpDetail>.Ok(detail));

        var request = new CreateFollowUpRequest(
            RelatedType.Customer, 10, null, null, FixedNowUtc.AddDays(3),
            "Check contract", FollowUpType.Meeting, null, null);

        // Act
        var result = await controller.Create(request, CancellationToken.None);

        // Assert
        var createdResult = result.ShouldBeOfType<CreatedResult>();
        createdResult.Location.ShouldBe("/api/followups/2");
        var dto = createdResult.Value.ShouldBeOfType<FollowUpDto>();
        dto.Id.ShouldBe(2);
        dto.Subject.ShouldBe("Check contract");
    }
}

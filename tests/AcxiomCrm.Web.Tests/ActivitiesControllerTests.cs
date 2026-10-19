using AcxiomCrm.Application.Abstractions;
using AcxiomCrm.Application.Activities;
using AcxiomCrm.Application.Audit;
using AcxiomCrm.Application.Common;
using AcxiomCrm.Application.Customers;
using AcxiomCrm.Application.Leads;
using AcxiomCrm.Application.Opportunities;
using AcxiomCrm.Application.Users;
using AcxiomCrm.Domain.Enums;
using AcxiomCrm.Domain.Security;
using AcxiomCrm.Web.Controllers;
using AcxiomCrm.Web.Services;
using AcxiomCrm.Web.ViewModels.Activities;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using NSubstitute;
using Shouldly;

namespace AcxiomCrm.Web.Tests;

public class ActivitiesControllerTests
{
    private static readonly DateTime FixedDate = new(2026, 10, 19, 10, 0, 0, DateTimeKind.Utc);
    private static readonly DateOnly Today = new(2026, 10, 19);

    private readonly IActivityService _activityService = Substitute.For<IActivityService>();
    private readonly ICustomerService _customerService = Substitute.For<ICustomerService>();
    private readonly ILeadService _leadService = Substitute.For<ILeadService>();
    private readonly IOpportunityService _opportunityService = Substitute.For<IOpportunityService>();
    private readonly IUserDirectory _userDirectory = Substitute.For<IUserDirectory>();
    private readonly IAuditQueryService _auditQueryService = Substitute.For<IAuditQueryService>();
    private readonly IClock _clock = Substitute.For<IClock>();
    private readonly ICurrentUser _currentUser = Substitute.For<ICurrentUser>();
    private readonly IFlash _flash = Substitute.For<IFlash>();

    public ActivitiesControllerTests()
    {
        _clock.Today.Returns(Today);
        _clock.UtcNow.Returns(FixedDate);
        _clock.ToUtcFromBusinessLocal(Arg.Any<DateTime>()).Returns(ci => (DateTime)ci[0]);
        _clock.BusinessDateOf(Arg.Any<DateTime>()).Returns(Today);
    }

    private ActivitiesController CreateController(string role = Roles.Admin, string userId = "user-1")
    {
        _currentUser.IsAuthenticated.Returns(true);
        _currentUser.Role.Returns(role);
        _currentUser.UserId.Returns(userId);

        var controller = new ActivitiesController(
            _activityService,
            _customerService,
            _leadService,
            _opportunityService,
            _userDirectory,
            _auditQueryService,
            _clock,
            _currentUser,
            _flash);

        var httpContext = new DefaultHttpContext();
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = httpContext
        };
        controller.TempData = new TempDataDictionary(httpContext, Substitute.For<ITempDataProvider>());
        return controller;
    }

    [Fact]
    public async Task Index_shows_table_with_paged_activities()
    {
        var controller = CreateController();
        var items = new List<ActivityListItem>
        {
            new(1, ActivityType.Call, "Discovery Call", "Notes", FixedDate, [new RelatedRef(RelatedType.Customer, 10)], "user-1", "User One", ActivityStatus.Completed)
        };

        _activityService.SearchAsync(Arg.Any<ActivityFilter>(), Arg.Any<PageQuery>(), Arg.Any<CancellationToken>())
            .Returns(new PagedResult<ActivityListItem>(items, 1, 25, 1));

        var result = await controller.Index();

        var viewResult = result.ShouldBeOfType<ViewResult>();
        var model = viewResult.Model.ShouldBeOfType<ActivityListViewModel>();
        model.Result.Items.Count.ShouldBe(1);
    }

    [Fact]
    public async Task Details_returns_view_when_found()
    {
        var controller = CreateController();
        var detail = new ActivityDetail(
            1, ActivityType.Call, "Discovery Call", "Detailed summary", FixedDate,
            10, "Customer 10", null, null, null, null, "user-1", "User One",
            ActivityStatus.Completed, FixedDate, [1, 2, 3]);

        _activityService.GetAsync(1, Arg.Any<CancellationToken>())
            .Returns(Result<ActivityDetail>.Ok(detail));

        _auditQueryService.ForRecordAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<PageQuery>(), Arg.Any<CancellationToken>())
            .Returns(new PagedResult<AuditListItem>([], 1, 50, 0));

        var result = await controller.Details(1);

        var viewResult = result.ShouldBeOfType<ViewResult>();
        var model = viewResult.Model.ShouldBeOfType<ActivityDetailsViewModel>();
        model.Detail.Subject.ShouldBe("Discovery Call");
    }

    [Fact]
    public async Task Details_returns_not_found_when_missing()
    {
        var controller = CreateController();
        _activityService.GetAsync(999, Arg.Any<CancellationToken>())
            .Returns(Result<ActivityDetail>.Fail(ErrorKind.NotFound));

        var result = await controller.Details(999);

        result.ShouldBeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task Create_get_prefills_related_info()
    {
        var controller = CreateController();
        _customerService.GetAsync(42, Arg.Any<CancellationToken>())
            .Returns(Result<CustomerDetail>.Ok(new CustomerDetail(42, "CUS-000042", "Customer 42", null, "c@e.com", "9876543210", null, null, null, CustomerStatus.Active, "user-1", "User 1", null, FixedDate, null, [])));

        var result = await controller.Create(RelatedType.Customer, 42);

        var viewResult = result.ShouldBeOfType<ViewResult>();
        var model = viewResult.Model.ShouldBeOfType<ActivityFormViewModel>();
        model.CustomerId.ShouldBe(42);
        model.CustomerName.ShouldBe("Customer 42");
    }

    [Fact]
    public async Task Create_post_valid_redirects_to_details()
    {
        var controller = CreateController();
        var model = new ActivityFormViewModel
        {
            Type = ActivityType.Meeting,
            Subject = "Demo Meeting",
            ActivityDate = Today,
            ActivityTime = new TimeOnly(14, 0),
            CustomerId = 10,
            Status = ActivityStatus.Completed
        };

        var created = new ActivityDetail(
            15, ActivityType.Meeting, "Demo Meeting", null, FixedDate,
            10, "Customer 10", null, null, null, null, "user-1", "User One",
            ActivityStatus.Completed, FixedDate, [1, 2, 3]);

        _activityService.CreateAsync(Arg.Any<CreateActivityCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result<ActivityDetail>.Ok(created));

        var result = await controller.Create(model);

        var redirect = result.ShouldBeOfType<RedirectToActionResult>();
        redirect.ActionName.ShouldBe("Details");
        redirect.RouteValues!["id"].ShouldBe(15);
        _flash.Received().Success("Activity logged.");
    }

    [Fact]
    public async Task Delete_post_redirects_to_index()
    {
        var controller = CreateController();
        _activityService.DeleteAsync(1, Arg.Any<CancellationToken>())
            .Returns(Result.Ok());

        var result = await controller.Delete(1);

        var redirect = result.ShouldBeOfType<RedirectToActionResult>();
        redirect.ActionName.ShouldBe("Index");
        _flash.Received().Success("Activity deleted.");
    }
}

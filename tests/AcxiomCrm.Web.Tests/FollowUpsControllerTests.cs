using AcxiomCrm.Application.Abstractions;
using AcxiomCrm.Application.Audit;
using AcxiomCrm.Application.Common;
using AcxiomCrm.Application.Customers;
using AcxiomCrm.Application.FollowUps;
using AcxiomCrm.Application.Leads;
using AcxiomCrm.Application.Opportunities;
using AcxiomCrm.Application.Users;
using AcxiomCrm.Domain.Enums;
using AcxiomCrm.Domain.Security;
using AcxiomCrm.Web.Controllers;
using AcxiomCrm.Web.Services;
using AcxiomCrm.Web.ViewModels.FollowUps;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using NSubstitute;
using Shouldly;

namespace AcxiomCrm.Web.Tests;

public class FollowUpsControllerTests
{
    private static readonly DateTime FixedDate = new(2026, 10, 19, 10, 0, 0, DateTimeKind.Utc);
    private static readonly DateOnly Today = new(2026, 10, 19);

    private readonly IFollowUpService _followUpService = Substitute.For<IFollowUpService>();
    private readonly ICustomerService _customerService = Substitute.For<ICustomerService>();
    private readonly ILeadService _leadService = Substitute.For<ILeadService>();
    private readonly IOpportunityService _opportunityService = Substitute.For<IOpportunityService>();
    private readonly IUserDirectory _userDirectory = Substitute.For<IUserDirectory>();
    private readonly IAuditQueryService _auditQueryService = Substitute.For<IAuditQueryService>();
    private readonly IClock _clock = Substitute.For<IClock>();
    private readonly ICurrentUser _currentUser = Substitute.For<ICurrentUser>();
    private readonly IFlash _flash = Substitute.For<IFlash>();

    public FollowUpsControllerTests()
    {
        _clock.Today.Returns(Today);
        _clock.UtcNow.Returns(FixedDate);
        _clock.ToUtcFromBusinessLocal(Arg.Any<DateTime>()).Returns(ci => (DateTime)ci[0]);
        _clock.BusinessDateOf(Arg.Any<DateTime>()).Returns(Today);
    }

    private FollowUpsController CreateController(string role = Roles.Admin, string userId = "user-1")
    {
        _currentUser.IsAuthenticated.Returns(true);
        _currentUser.Role.Returns(role);
        _currentUser.UserId.Returns(userId);

        var controller = new FollowUpsController(
            _followUpService,
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
    public async Task Index_shows_table_with_paged_followups_and_counts()
    {
        var controller = CreateController(Roles.SalesExecutive);
        var items = new List<FollowUpListItem>
        {
            new(1, RelatedType.Customer, 10, "Cust 10", false, FixedDate.AddDays(1), "Follow-up 1", FollowUpType.Call, FollowUpStatus.Planned, false, false, "user-1", "User 1", 0)
        };

        _followUpService.SearchAsync(Arg.Any<FollowUpFilter>(), Arg.Any<PageQuery>(), Arg.Any<CancellationToken>())
            .Returns(new PagedResult<FollowUpListItem>(items, 1, 25, 1));

        _followUpService.GetNotificationCountsAsync(Arg.Any<CancellationToken>())
            .Returns(new NotificationCounts(1, 2));

        var result = await controller.Index();

        var viewResult = result.ShouldBeOfType<ViewResult>();
        var model = viewResult.Model.ShouldBeOfType<FollowUpListViewModel>();
        model.Result.Items.Count.ShouldBe(1);
        model.Counts.Overdue.ShouldBe(1);
        model.Counts.DueToday.ShouldBe(2);
    }

    [Fact]
    public async Task Details_returns_view_when_found()
    {
        var controller = CreateController();
        var detail = new FollowUpDetail(
            1, RelatedType.Customer, 10, "Cust 10", FixedDate.AddDays(1), "Call Client",
            FollowUpType.Call, FollowUpStatus.Planned, "Remarks", "user-1", "User One",
            null, 0, false, false, FixedDate, [1, 2, 3]);

        _followUpService.GetAsync(1, Arg.Any<CancellationToken>())
            .Returns(Result<FollowUpDetail>.Ok(detail));

        _auditQueryService.ForRecordAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<PageQuery>(), Arg.Any<CancellationToken>())
            .Returns(new PagedResult<AuditListItem>([], 1, 50, 0));

        var result = await controller.Details(1);

        var viewResult = result.ShouldBeOfType<ViewResult>();
        var model = viewResult.Model.ShouldBeOfType<FollowUpDetailsViewModel>();
        model.Detail.Subject.ShouldBe("Call Client");
        model.CanComplete.ShouldBeTrue();
    }

    [Fact]
    public async Task Details_returns_not_found_when_missing()
    {
        var controller = CreateController();
        _followUpService.GetAsync(999, Arg.Any<CancellationToken>())
            .Returns(Result<FollowUpDetail>.Fail(ErrorKind.NotFound));

        var result = await controller.Details(999);

        result.ShouldBeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task Create_get_prefills_related_info()
    {
        var controller = CreateController();
        _customerService.GetAsync(42, Arg.Any<CancellationToken>())
            .Returns(Result<CustomerDetail>.Ok(new CustomerDetail(42, "CUS-000042", "Prefilled Cust", null, "c@e.com", "9876543210", null, null, null, CustomerStatus.Active, "user-1", "User 1", null, FixedDate, null, [])));

        var result = await controller.Create(RelatedType.Customer, 42);

        var viewResult = result.ShouldBeOfType<ViewResult>();
        var model = viewResult.Model.ShouldBeOfType<FollowUpFormViewModel>();
        model.RelatedType.ShouldBe(RelatedType.Customer);
        model.RelatedId.ShouldBe(42);
        model.RelatedName.ShouldBe("Prefilled Cust");
    }

    [Fact]
    public async Task Create_post_valid_redirects_to_details()
    {
        var controller = CreateController();
        var model = new FollowUpFormViewModel
        {
            RelatedType = RelatedType.Customer,
            RelatedId = 10,
            Subject = "Test Schedule",
            Type = FollowUpType.Call,
            FollowUpDate = Today.AddDays(2),
            FollowUpTime = new TimeOnly(11, 0)
        };

        var created = new FollowUpDetail(
            5, RelatedType.Customer, 10, "Cust 10", FixedDate.AddDays(2), "Test Schedule",
            FollowUpType.Call, FollowUpStatus.Planned, null, "user-1", "User One",
            null, 0, false, false, FixedDate, [1, 2, 3]);

        _followUpService.CreateAsync(Arg.Any<CreateFollowUpCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result<FollowUpDetail>.Ok(created));

        var result = await controller.Create(model);

        var redirect = result.ShouldBeOfType<RedirectToActionResult>();
        redirect.ActionName.ShouldBe("Details");
        redirect.RouteValues!["id"].ShouldBe(5);
        _flash.Received().Success("Follow-up scheduled.");
    }

    [Fact]
    public async Task Complete_post_redirects_to_details()
    {
        var controller = CreateController();
        var model = new CompleteFollowUpViewModel
        {
            Id = 1,
            Remarks = "All done",
            LogActivity = true,
            ScheduleNext = false
        };

        var completed = new FollowUpDetail(
            1, RelatedType.Customer, 10, "Cust 10", FixedDate, "Call Client",
            FollowUpType.Call, FollowUpStatus.Completed, "All done", "user-1", "User One",
            FixedDate, 0, false, false, FixedDate, [1, 2, 3]);

        _followUpService.CompleteAsync(1, Arg.Any<CompleteFollowUpCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result<FollowUpDetail>.Ok(completed));

        var result = await controller.Complete(1, model);

        var redirect = result.ShouldBeOfType<RedirectToActionResult>();
        redirect.ActionName.ShouldBe("Details");
        _flash.Received().Success("Follow-up marked as completed.");
    }

    [Fact]
    public async Task Complete_with_schedule_next_redirects_to_create()
    {
        var controller = CreateController();
        var model = new CompleteFollowUpViewModel
        {
            Id = 1,
            Remarks = "All done, schedule next",
            LogActivity = true,
            ScheduleNext = true
        };

        var completed = new FollowUpDetail(
            1, RelatedType.Customer, 10, "Cust 10", FixedDate, "Call Client",
            FollowUpType.Call, FollowUpStatus.Completed, "All done, schedule next", "user-1", "User One",
            FixedDate, 0, false, false, FixedDate, [1, 2, 3]);

        _followUpService.CompleteAsync(1, Arg.Any<CompleteFollowUpCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result<FollowUpDetail>.Ok(completed));

        var result = await controller.Complete(1, model);

        var redirect = result.ShouldBeOfType<RedirectToActionResult>();
        redirect.ActionName.ShouldBe("Create");
        redirect.RouteValues!["relatedType"].ShouldBe(RelatedType.Customer);
        redirect.RouteValues["relatedId"].ShouldBe(10);
    }

    [Fact]
    public async Task Delete_post_redirects_to_index()
    {
        var controller = CreateController();
        _followUpService.DeleteAsync(1, Arg.Any<CancellationToken>())
            .Returns(Result.Ok());

        var result = await controller.Delete(1);

        var redirect = result.ShouldBeOfType<RedirectToActionResult>();
        redirect.ActionName.ShouldBe("Index");
        _flash.Received().Success("Follow-up deleted.");
    }
}

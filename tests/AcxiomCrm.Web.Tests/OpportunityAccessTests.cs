using System.Reflection;
using AcxiomCrm.Application.Abstractions;
using AcxiomCrm.Application.Audit;
using AcxiomCrm.Application.Common;
using AcxiomCrm.Application.Customers;
using AcxiomCrm.Application.Opportunities;
using AcxiomCrm.Application.Users;
using AcxiomCrm.Domain.Enums;
using AcxiomCrm.Domain.Security;
using AcxiomCrm.Web.Controllers;
using AcxiomCrm.Web.Services;
using AcxiomCrm.Web.ViewModels.Opportunities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using NSubstitute;
using Shouldly;

namespace AcxiomCrm.Web.Tests;

public class OpportunityAccessTests
{
    private readonly IOpportunityService _opportunityService = Substitute.For<IOpportunityService>();
    private readonly ICustomerService _customerService = Substitute.For<ICustomerService>();
    private readonly IUserDirectory _userDirectory = Substitute.For<IUserDirectory>();
    private readonly IAuditQueryService _auditQueryService = Substitute.For<IAuditQueryService>();
    private readonly IClock _clock = Substitute.For<IClock>();
    private readonly ICurrentUser _currentUser = Substitute.For<ICurrentUser>();
    private readonly IFlash _flash = Substitute.For<IFlash>();

    private OpportunitiesController CreateController(string role = Roles.Admin)
    {
        _currentUser.Role.Returns(role);
        _currentUser.IsAuthenticated.Returns(true);
        _currentUser.UserId.Returns("user-1");

        var controller = new OpportunitiesController(
            _opportunityService,
            _customerService,
            _userDirectory,
            _auditQueryService,
            _clock,
            _currentUser,
            _flash);

        var httpContext = new DefaultHttpContext();
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };
        controller.TempData = new TempDataDictionary(httpContext, Substitute.For<ITempDataProvider>());
        return controller;
    }

    [Fact]
    public void Controller_and_actions_have_expected_authorize_policies()
    {
        var controllerType = typeof(OpportunitiesController);

        var controllerAuth = controllerType.GetCustomAttribute<AuthorizeAttribute>();
        controllerAuth.ShouldNotBeNull();
        controllerAuth.Policy.ShouldBe(Permissions.OpportunitiesView);

        var detailsGet = controllerType.GetMethod(nameof(OpportunitiesController.Details), [typeof(int), typeof(CancellationToken)]);
        detailsGet.ShouldNotBeNull();

        var createGet = controllerType.GetMethod(nameof(OpportunitiesController.Create), [typeof(int?), typeof(CancellationToken)]);
        createGet?.GetCustomAttribute<AuthorizeAttribute>()?.Policy.ShouldBe(Permissions.OpportunitiesCreate);

        var createPost = controllerType.GetMethod(nameof(OpportunitiesController.Create), [typeof(OpportunityFormViewModel), typeof(CancellationToken)]);
        createPost?.GetCustomAttribute<AuthorizeAttribute>()?.Policy.ShouldBe(Permissions.OpportunitiesCreate);

        var editGet = controllerType.GetMethod(nameof(OpportunitiesController.Edit), [typeof(int), typeof(CancellationToken)]);
        editGet?.GetCustomAttribute<AuthorizeAttribute>()?.Policy.ShouldBe(Permissions.OpportunitiesEdit);

        var editPost = controllerType.GetMethod(nameof(OpportunitiesController.Edit), [typeof(int), typeof(OpportunityFormViewModel), typeof(CancellationToken)]);
        editPost?.GetCustomAttribute<AuthorizeAttribute>()?.Policy.ShouldBe(Permissions.OpportunitiesEdit);

        var deletePost = controllerType.GetMethod(nameof(OpportunitiesController.Delete), [typeof(int), typeof(CancellationToken)]);
        deletePost?.GetCustomAttribute<AuthorizeAttribute>()?.Policy.ShouldBe(Permissions.OpportunitiesDelete);

        var reopenPost = controllerType.GetMethod(nameof(OpportunitiesController.Reopen), [typeof(int), typeof(string), typeof(CancellationToken)]);
        reopenPost?.GetCustomAttribute<AuthorizeAttribute>()?.Policy.ShouldBe(Permissions.OpportunitiesReopen);

        var changeStagePost = controllerType.GetMethod(nameof(OpportunitiesController.ChangeStage), [typeof(int), typeof(ChangeStageViewModel), typeof(CancellationToken)]);
        changeStagePost?.GetCustomAttribute<AuthorizeAttribute>()?.Policy.ShouldBe(Permissions.OpportunitiesEdit);
    }

    [Fact]
    public async Task Executive_cannot_reopen_or_delete()
    {
        var controller = CreateController(Roles.SalesExecutive);

        _opportunityService.ReopenAsync(1, Arg.Any<byte[]>())
            .Returns(Result<OpportunityDetail>.Fail(ErrorKind.Forbidden));
        _opportunityService.DeleteAsync(1)
            .Returns(Result.Fail(ErrorKind.Forbidden));

        var reopenResult = await controller.Reopen(1, "rowversion");
        reopenResult.ShouldBeOfType<ForbidResult>();

        var deleteResult = await controller.Delete(1);
        deleteResult.ShouldBeOfType<ForbidResult>();
    }

    [Fact]
    public async Task Manager_can_reopen_and_delete()
    {
        var controller = CreateController(Roles.Manager);

        var reopened = new OpportunityDetail(1, "Opp", 10, "Cust", "C-01", null, 1000m, 75, 750m,
            OpportunityStage.Negotiation, OpportunityStatus.Open, new DateOnly(2026, 12, 1), null, null, null, "user-1", "John", null, new DateTime(2026, 10, 18, 10, 0, 0, DateTimeKind.Utc), null, [1]);
        _opportunityService.ReopenAsync(1, Arg.Any<byte[]>())
            .Returns(Result<OpportunityDetail>.Ok(reopened));
        _opportunityService.DeleteAsync(1)
            .Returns(Result.Ok());

        var reopenResult = await controller.Reopen(1, Convert.ToBase64String(new byte[] { 1 }));
        reopenResult.ShouldBeOfType<RedirectToActionResult>();

        var deleteResult = await controller.Delete(1);
        deleteResult.ShouldBeOfType<RedirectToActionResult>();
    }

    [Fact]
    public async Task Idor_returns_not_found()
    {
        var controller = CreateController(Roles.SalesExecutive);

        _opportunityService.GetAsync(99)
            .Returns(Result<OpportunityDetail>.Fail(ErrorKind.NotFound));
        _opportunityService.DeleteAsync(99)
            .Returns(Result.Fail(ErrorKind.NotFound));

        var getResult = await controller.Details(99);
        getResult.ShouldBeOfType<NotFoundResult>();

        var deleteResult = await controller.Delete(99);
        deleteResult.ShouldBeOfType<NotFoundResult>();
    }
}

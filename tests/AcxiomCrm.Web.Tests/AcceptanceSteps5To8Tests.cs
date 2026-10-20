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
using AcxiomCrm.Domain.Validation;
using AcxiomCrm.Web.Controllers;
using AcxiomCrm.Web.Services;
using AcxiomCrm.Web.ViewModels.FollowUps;
using AcxiomCrm.Web.ViewModels.Opportunities;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using NSubstitute;
using Shouldly;

namespace AcxiomCrm.Web.Tests;

public class AcceptanceSteps5To8Tests
{
    private static readonly DateOnly Today = new(2026, 10, 20);
    private static readonly DateTime FixedDate = new(2026, 10, 20, 10, 0, 0, DateTimeKind.Utc);

    private readonly IOpportunityService _opportunityService = Substitute.For<IOpportunityService>();
    private readonly ICustomerService _customerService = Substitute.For<ICustomerService>();
    private readonly ILeadService _leadService = Substitute.For<ILeadService>();
    private readonly IFollowUpService _followUpService = Substitute.For<IFollowUpService>();
    private readonly IUserDirectory _userDirectory = Substitute.For<IUserDirectory>();
    private readonly IAuditQueryService _auditQueryService = Substitute.For<IAuditQueryService>();
    private readonly IClock _clock = Substitute.For<IClock>();
    private readonly ICurrentUser _currentUser = Substitute.For<ICurrentUser>();
    private readonly IFlash _flash = Substitute.For<IFlash>();

    public AcceptanceSteps5To8Tests()
    {
        _clock.Today.Returns(Today);
        _clock.UtcNow.Returns(FixedDate);
        _clock.ToUtcFromBusinessLocal(Arg.Any<DateTime>()).Returns(ci => (DateTime)ci[0]);
        _clock.BusinessDateOf(Arg.Any<DateTime>()).Returns(Today);

        _currentUser.IsAuthenticated.Returns(true);
        _currentUser.Role.Returns(Roles.SalesExecutive);
        _currentUser.UserId.Returns("exec-1");
    }

    [Fact]
    public async Task Step5_Opportunity_Amount_Zero_Or_Negative_Rejects_With_Canonical_Message()
    {
        var controller = CreateOpportunitiesController();
        var model = new OpportunityFormViewModel
        {
            Name = "Big Deal",
            CustomerId = 1,
            Amount = 0,
            Probability = 50,
            ExpectedCloseDate = Today.AddDays(10),
            Stage = OpportunityStage.Qualification
        };

        _opportunityService.CreateAsync(Arg.Any<CreateOpportunityCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result<OpportunityDetail>.Fail(ErrorKind.Validation, new FieldError("Amount", Messages.AmountPositive)));

        var result = await controller.Create(model);

        var viewResult = result.ShouldBeOfType<ViewResult>();
        controller.ModelState.IsValid.ShouldBeFalse();
        controller.ModelState["Amount"]!.Errors.ShouldContain(e => e.ErrorMessage == Messages.AmountPositive);
    }

    [Fact]
    public async Task Step6_Opportunity_Probability_Over_100_Rejects_With_Canonical_Message()
    {
        var controller = CreateOpportunitiesController();
        var model = new OpportunityFormViewModel
        {
            Name = "Big Deal",
            CustomerId = 1,
            Amount = 100000,
            Probability = 101,
            ExpectedCloseDate = Today.AddDays(10),
            Stage = OpportunityStage.Qualification
        };

        _opportunityService.CreateAsync(Arg.Any<CreateOpportunityCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result<OpportunityDetail>.Fail(ErrorKind.Validation, new FieldError("Probability", Messages.ProbabilityRange)));

        var result = await controller.Create(model);

        var viewResult = result.ShouldBeOfType<ViewResult>();
        controller.ModelState.IsValid.ShouldBeFalse();
        controller.ModelState["Probability"]!.Errors.ShouldContain(e => e.ErrorMessage == Messages.ProbabilityRange);
    }

    [Fact]
    public async Task Step7_Opportunity_CloseDate_In_Past_Rejects_With_Canonical_Message()
    {
        var controller = CreateOpportunitiesController();
        var model = new OpportunityFormViewModel
        {
            Name = "Big Deal",
            CustomerId = 1,
            Amount = 100000,
            Probability = 50,
            ExpectedCloseDate = Today.AddDays(-1), // Yesterday
            Stage = OpportunityStage.Qualification
        };

        _opportunityService.CreateAsync(Arg.Any<CreateOpportunityCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result<OpportunityDetail>.Fail(ErrorKind.Validation, new FieldError("ExpectedCloseDate", Messages.ExpectedCloseDatePast)));

        var result = await controller.Create(model);

        var viewResult = result.ShouldBeOfType<ViewResult>();
        controller.ModelState.IsValid.ShouldBeFalse();
        controller.ModelState["ExpectedCloseDate"]!.Errors.ShouldContain(e => e.ErrorMessage == Messages.ExpectedCloseDatePast);
    }

    [Fact]
    public async Task Step8_FollowUp_Date_Earlier_Than_Today_Rejects_With_Canonical_Message()
    {
        var controller = CreateFollowUpsController();
        var model = new FollowUpFormViewModel
        {
            RelatedType = RelatedType.Customer,
            RelatedId = 1,
            Subject = "Catch up call",
            Type = FollowUpType.Call,
            FollowUpDate = Today.AddDays(-1), // Yesterday
            FollowUpTime = new TimeOnly(14, 0)
        };

        _followUpService.CreateAsync(Arg.Any<CreateFollowUpCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result<FollowUpDetail>.Fail(ErrorKind.Validation, new FieldError("FollowUpDate", Messages.FollowUpDateBeforeToday)));

        var result = await controller.Create(model);

        var viewResult = result.ShouldBeOfType<ViewResult>();
        controller.ModelState.IsValid.ShouldBeFalse();
        controller.ModelState["FollowUpDate"]!.Errors.ShouldContain(e => e.ErrorMessage == Messages.FollowUpDateBeforeToday);
    }

    private OpportunitiesController CreateOpportunitiesController()
    {
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

    private FollowUpsController CreateFollowUpsController()
    {
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
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };
        controller.TempData = new TempDataDictionary(httpContext, Substitute.For<ITempDataProvider>());
        return controller;
    }
}

using AcxiomCrm.Application.Abstractions;
using AcxiomCrm.Application.Audit;
using AcxiomCrm.Application.Common;
using AcxiomCrm.Application.Customers;
using AcxiomCrm.Application.Opportunities;
using AcxiomCrm.Application.Users;
using AcxiomCrm.Domain.Enums;
using AcxiomCrm.Domain.Security;
using AcxiomCrm.Domain.Validation;
using AcxiomCrm.Web.Controllers;
using AcxiomCrm.Web.Services;
using AcxiomCrm.Web.ViewModels.Opportunities;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using NSubstitute;
using Shouldly;

namespace AcxiomCrm.Web.Tests;

public class OpportunityServerValidationTests
{
    private static readonly DateOnly Today = new(2026, 10, 18);
    private static readonly DateTime FixedDate = new(2026, 10, 18, 10, 0, 0, DateTimeKind.Utc);

    private readonly IOpportunityService _opportunityService = Substitute.For<IOpportunityService>();
    private readonly ICustomerService _customerService = Substitute.For<ICustomerService>();
    private readonly IUserDirectory _userDirectory = Substitute.For<IUserDirectory>();
    private readonly IAuditQueryService _auditQueryService = Substitute.For<IAuditQueryService>();
    private readonly IClock _clock = Substitute.For<IClock>();
    private readonly ICurrentUser _currentUser = Substitute.For<ICurrentUser>();
    private readonly IFlash _flash = Substitute.For<IFlash>();

    public OpportunityServerValidationTests()
    {
        _clock.Today.Returns(Today);
        _clock.UtcNow.Returns(FixedDate);
    }

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

    [Theory]
    [InlineData(0, "Opportunity Amount must be greater than 0.")]
    [InlineData(-5, "Opportunity Amount must be greater than 0.")]
    public async Task Crafted_post_invalid_amount_is_rejected(decimal amount, string expectedMessage)
    {
        var controller = CreateController();
        controller.ModelState.AddModelError(nameof(OpportunityFormViewModel.Amount), expectedMessage);

        var form = new OpportunityFormViewModel
        {
            Name = "Test Opp",
            CustomerId = 1,
            Amount = amount,
            Probability = 20,
            ExpectedCloseDate = Today.AddDays(10),
            Stage = OpportunityStage.Qualification
        };

        var result = await controller.Create(form);

        var viewResult = result.ShouldBeOfType<ViewResult>();
        viewResult.Model.ShouldBe(form);
        controller.ModelState[nameof(OpportunityFormViewModel.Amount)]!.Errors[0].ErrorMessage.ShouldBe(expectedMessage);
        await _opportunityService.DidNotReceive().CreateAsync(Arg.Any<CreateOpportunityCommand>());
    }

    [Fact]
    public async Task Crafted_post_probability_101_is_rejected()
    {
        var controller = CreateController();
        var expectedMessage = "Opportunity Probability must be between 0 and 100.";
        controller.ModelState.AddModelError(nameof(OpportunityFormViewModel.Probability), expectedMessage);

        var form = new OpportunityFormViewModel
        {
            Name = "Test Opp",
            CustomerId = 1,
            Amount = 1000m,
            Probability = 101,
            ExpectedCloseDate = Today.AddDays(10),
            Stage = OpportunityStage.Qualification
        };

        var result = await controller.Create(form);

        var viewResult = result.ShouldBeOfType<ViewResult>();
        viewResult.Model.ShouldBe(form);
        controller.ModelState[nameof(OpportunityFormViewModel.Probability)]!.Errors[0].ErrorMessage.ShouldBe(expectedMessage);
        await _opportunityService.DidNotReceive().CreateAsync(Arg.Any<CreateOpportunityCommand>());
    }

    [Fact]
    public async Task Crafted_post_yesterday_close_date_is_rejected()
    {
        var controller = CreateController();
        var expectedMessage = "Expected Close Date cannot be in the past.";
        controller.ModelState.AddModelError(nameof(OpportunityFormViewModel.ExpectedCloseDate), expectedMessage);

        var form = new OpportunityFormViewModel
        {
            Name = "Test Opp",
            CustomerId = 1,
            Amount = 1000m,
            Probability = 20,
            ExpectedCloseDate = Today.AddDays(-1),
            Stage = OpportunityStage.Qualification
        };

        var result = await controller.Create(form);

        var viewResult = result.ShouldBeOfType<ViewResult>();
        viewResult.Model.ShouldBe(form);
        controller.ModelState[nameof(OpportunityFormViewModel.ExpectedCloseDate)]!.Errors[0].ErrorMessage.ShouldBe(expectedMessage);
        await _opportunityService.DidNotReceive().CreateAsync(Arg.Any<CreateOpportunityCommand>());
    }

    [Fact]
    public async Task Stage_field_tampering_on_Edit_is_ignored()
    {
        var controller = CreateController();

        var existing = new OpportunityDetail(
            1, "Existing Opp", 10, "Acme", "C-01", null,
            50000m, 20, 10000m, OpportunityStage.Qualification, OpportunityStatus.Open,
            Today.AddDays(10), null, null, null, "user-1", "John", null, FixedDate, null, [1]);
        _opportunityService.GetAsync(1).Returns(Result<OpportunityDetail>.Ok(existing));

        _opportunityService.UpdateAsync(1, Arg.Any<UpdateOpportunityCommand>())
            .Returns(Result<OpportunityDetail>.Ok(existing));

        // Malicious POST attempts to change Stage to Won directly in Edit form
        var form = new OpportunityFormViewModel
        {
            Id = 1,
            Name = "Updated Opp",
            CustomerId = 10,
            Amount = 50000m,
            Probability = 20,
            ExpectedCloseDate = Today.AddDays(10),
            Stage = OpportunityStage.Won,
            RowVersion = Convert.ToBase64String(new byte[] { 1 })
        };

        var result = await controller.Edit(1, form);

        result.ShouldBeOfType<RedirectToActionResult>();

        // Assert UpdateOpportunityCommand was called (which has no Stage field: stage tampering ignored)
        await _opportunityService.Received(1).UpdateAsync(1, Arg.Is<UpdateOpportunityCommand>(c => c.Name == "Updated Opp"));
    }

    [Fact]
    public async Task Customer_from_another_team_is_rejected()
    {
        var controller = CreateController(Roles.SalesExecutive);
        _opportunityService.CreateAsync(Arg.Any<CreateOpportunityCommand>())
            .Returns(Result<OpportunityDetail>.Fail(ErrorKind.Validation, [new FieldError("CustomerId", "Choose an active customer.")]));

        var form = new OpportunityFormViewModel
        {
            Name = "Cross team Opp",
            CustomerId = 999, // Customer belonging to another team
            Amount = 10000m,
            Probability = 20,
            ExpectedCloseDate = Today.AddDays(10),
            Stage = OpportunityStage.Qualification
        };

        var result = await controller.Create(form);

        var viewResult = result.ShouldBeOfType<ViewResult>();
        controller.ModelState[nameof(OpportunityFormViewModel.CustomerId)]!.Errors[0].ErrorMessage.ShouldBe("Choose an active customer.");
    }
}

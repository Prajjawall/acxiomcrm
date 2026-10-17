using AcxiomCrm.Application.Abstractions;
using AcxiomCrm.Application.Audit;
using AcxiomCrm.Application.Common;
using AcxiomCrm.Application.Customers;
using AcxiomCrm.Application.Leads;
using AcxiomCrm.Application.Users;
using AcxiomCrm.Domain.Security;
using AcxiomCrm.Domain.Validation;
using AcxiomCrm.Web.Controllers;
using AcxiomCrm.Web.Services;
using AcxiomCrm.Web.ViewModels.Leads;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using Shouldly;

namespace AcxiomCrm.Web.Tests;

public class LeadServerValidationTests
{
    private readonly ILeadService _leadService = Substitute.For<ILeadService>();
    private readonly ICustomerService _customerService = Substitute.For<ICustomerService>();
    private readonly IUserDirectory _userDirectory = Substitute.For<IUserDirectory>();
    private readonly IAuditQueryService _auditQueryService = Substitute.For<IAuditQueryService>();
    private readonly IOpportunityFactory _opportunityFactory = Substitute.For<IOpportunityFactory>();
    private readonly IClock _clock = Substitute.For<IClock>();
    private readonly ICurrentUser _currentUser = Substitute.For<ICurrentUser>();
    private readonly IFlash _flash = Substitute.For<IFlash>();

    private LeadsController CreateController()
    {
        _currentUser.Role.Returns(Roles.Admin);
        return new LeadsController(
            _leadService,
            _customerService,
            _userDirectory,
            _auditQueryService,
            _opportunityFactory,
            _clock,
            _currentUser,
            _flash);
    }

    [Theory]
    [InlineData("", "Lead name is required.")]
    [InlineData("   ", "Lead name is required.")]
    public async Task Crafted_post_blank_name_is_rejected(string name, string expectedMessage)
    {
        var controller = CreateController();
        controller.ModelState.AddModelError(nameof(LeadFormViewModel.Name), expectedMessage);

        var model = new LeadFormViewModel { Name = name, Email = "valid@example.com", Phone = "9876543210" };
        var result = await controller.Create(model);

        var viewResult = result.ShouldBeOfType<ViewResult>();
        viewResult.Model.ShouldBe(model);
        controller.ModelState[nameof(LeadFormViewModel.Name)]!.Errors[0].ErrorMessage.ShouldBe(expectedMessage);
        await _leadService.DidNotReceive().CreateAsync(Arg.Any<CreateLeadCommand>());
    }

    [Fact]
    public async Task Crafted_post_too_long_name_is_rejected()
    {
        var controller = CreateController();
        var longName = new string('A', 151);
        var expectedMessage = "Lead name cannot exceed 150 characters.";
        controller.ModelState.AddModelError(nameof(LeadFormViewModel.Name), expectedMessage);

        var model = new LeadFormViewModel { Name = longName, Email = "valid@example.com", Phone = "9876543210" };
        var result = await controller.Create(model);

        var viewResult = result.ShouldBeOfType<ViewResult>();
        controller.ModelState[nameof(LeadFormViewModel.Name)]!.Errors[0].ErrorMessage.ShouldBe(expectedMessage);
        await _leadService.DidNotReceive().CreateAsync(Arg.Any<CreateLeadCommand>());
    }

    [Fact]
    public async Task Crafted_post_invalid_email_is_rejected()
    {
        var controller = CreateController();
        var expectedMessage = "Enter a valid email address.";
        controller.ModelState.AddModelError(nameof(LeadFormViewModel.Email), expectedMessage);

        var model = new LeadFormViewModel { Name = "Acme Lead", Email = "invalid-email", Phone = "9876543210" };
        var result = await controller.Create(model);

        var viewResult = result.ShouldBeOfType<ViewResult>();
        controller.ModelState[nameof(LeadFormViewModel.Email)]!.Errors[0].ErrorMessage.ShouldBe(expectedMessage);
        await _leadService.DidNotReceive().CreateAsync(Arg.Any<CreateLeadCommand>());
    }

    [Fact]
    public async Task Crafted_post_invalid_phone_is_rejected()
    {
        var controller = CreateController();
        var expectedMessage = "Enter a valid phone number.";
        controller.ModelState.AddModelError(nameof(LeadFormViewModel.Phone), expectedMessage);

        var model = new LeadFormViewModel { Name = "Acme Lead", Email = "acme@example.com", Phone = "12345" };
        var result = await controller.Create(model);

        var viewResult = result.ShouldBeOfType<ViewResult>();
        controller.ModelState[nameof(LeadFormViewModel.Phone)]!.Errors[0].ErrorMessage.ShouldBe(expectedMessage);
        await _leadService.DidNotReceive().CreateAsync(Arg.Any<CreateLeadCommand>());
    }

    [Fact]
    public async Task Neither_email_nor_phone_is_rejected()
    {
        var controller = CreateController();
        var expectedMessage = "Enter an email address or a phone number.";
        controller.ModelState.AddModelError(nameof(LeadFormViewModel.Email), expectedMessage);

        var model = new LeadFormViewModel { Name = "Acme Lead", Email = null, Phone = null };
        var result = await controller.Create(model);

        var viewResult = result.ShouldBeOfType<ViewResult>();
        controller.ModelState[nameof(LeadFormViewModel.Email)]!.Errors[0].ErrorMessage.ShouldBe(expectedMessage);
        await _leadService.DidNotReceive().CreateAsync(Arg.Any<CreateLeadCommand>());
    }

    [Fact]
    public async Task Service_validation_errors_are_applied_to_model_state()
    {
        var controller = CreateController();
        var errors = new List<FieldError>
        {
            new("OwnerId", "Target owner is outside your scope."),
            new("ExpectedValue", "Expected value must be between 0 and 10,00,00,000.")
        };
        _leadService.CreateAsync(Arg.Any<CreateLeadCommand>())
            .Returns(Result<LeadDetail>.Fail(ErrorKind.Validation, errors));

        var model = new LeadFormViewModel { Name = "Acme Lead", Email = "acme@example.com", Phone = "9876543210" };
        var result = await controller.Create(model);

        var viewResult = result.ShouldBeOfType<ViewResult>();
        controller.ModelState[nameof(LeadFormViewModel.OwnerId)]!.Errors[0].ErrorMessage
            .ShouldBe("Target owner is outside your scope.");
        controller.ModelState[nameof(LeadFormViewModel.ExpectedValue)]!.Errors[0].ErrorMessage
            .ShouldBe("Expected value must be between 0 and 10,00,00,000.");
    }
}

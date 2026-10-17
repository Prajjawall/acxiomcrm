using AcxiomCrm.Application.Abstractions;
using AcxiomCrm.Application.Audit;
using AcxiomCrm.Application.Common;
using AcxiomCrm.Application.Customers;
using AcxiomCrm.Application.Users;
using AcxiomCrm.Domain.Security;
using AcxiomCrm.Domain.Validation;
using AcxiomCrm.Web.Controllers;
using AcxiomCrm.Web.Services;
using AcxiomCrm.Web.ViewModels.Customers;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using Shouldly;

namespace AcxiomCrm.Web.Tests;

public class CustomerServerValidationTests
{
    private readonly ICustomerService _customerService = Substitute.For<ICustomerService>();
    private readonly IUserDirectory _userDirectory = Substitute.For<IUserDirectory>();
    private readonly IAuditQueryService _auditQueryService = Substitute.For<IAuditQueryService>();
    private readonly ICurrentUser _currentUser = Substitute.For<ICurrentUser>();
    private readonly IFlash _flash = Substitute.For<IFlash>();

    private CustomersController CreateController()
    {
        _currentUser.Role.Returns(Roles.Admin);
        return new CustomersController(
            _customerService,
            _userDirectory,
            _auditQueryService,
            _currentUser,
            _flash);
    }

    [Theory]
    [InlineData("", "Customer name is required.")]
    [InlineData("   ", "Customer name is required.")]
    public async Task Crafted_post_blank_name_is_rejected(string name, string expectedMessage)
    {
        var controller = CreateController();
        controller.ModelState.AddModelError(nameof(CustomerFormViewModel.Name), expectedMessage);

        var model = new CustomerFormViewModel { Name = name, Email = "valid@example.com", Phone = "9876543210" };
        var result = await controller.Create(model);

        var viewResult = result.ShouldBeOfType<ViewResult>();
        viewResult.Model.ShouldBe(model);
        controller.ModelState[nameof(CustomerFormViewModel.Name)]!.Errors[0].ErrorMessage.ShouldBe(expectedMessage);
        await _customerService.DidNotReceive().CreateAsync(Arg.Any<CreateCustomerCommand>());
    }

    [Fact]
    public async Task Crafted_post_too_long_name_is_rejected()
    {
        var controller = CreateController();
        var longName = new string('A', 151);
        var expectedMessage = "Customer name must not exceed 150 characters.";
        controller.ModelState.AddModelError(nameof(CustomerFormViewModel.Name), expectedMessage);

        var model = new CustomerFormViewModel { Name = longName, Email = "valid@example.com", Phone = "9876543210" };
        var result = await controller.Create(model);

        var viewResult = result.ShouldBeOfType<ViewResult>();
        controller.ModelState[nameof(CustomerFormViewModel.Name)]!.Errors[0].ErrorMessage.ShouldBe(expectedMessage);
        await _customerService.DidNotReceive().CreateAsync(Arg.Any<CreateCustomerCommand>());
    }

    [Fact]
    public async Task Crafted_post_invalid_email_is_rejected()
    {
        var controller = CreateController();
        var expectedMessage = "Enter a valid email address.";
        controller.ModelState.AddModelError(nameof(CustomerFormViewModel.Email), expectedMessage);

        var model = new CustomerFormViewModel { Name = "Acme", Email = "abc", Phone = "9876543210" };
        var result = await controller.Create(model);

        var viewResult = result.ShouldBeOfType<ViewResult>();
        controller.ModelState[nameof(CustomerFormViewModel.Email)]!.Errors[0].ErrorMessage.ShouldBe(expectedMessage);
        await _customerService.DidNotReceive().CreateAsync(Arg.Any<CreateCustomerCommand>());
    }

    [Fact]
    public async Task Crafted_post_invalid_phone_is_rejected()
    {
        var controller = CreateController();
        var expectedMessage = "Enter a valid phone number.";
        controller.ModelState.AddModelError(nameof(CustomerFormViewModel.Phone), expectedMessage);

        var model = new CustomerFormViewModel { Name = "Acme", Email = "acme@example.com", Phone = "12345" };
        var result = await controller.Create(model);

        var viewResult = result.ShouldBeOfType<ViewResult>();
        controller.ModelState[nameof(CustomerFormViewModel.Phone)]!.Errors[0].ErrorMessage.ShouldBe(expectedMessage);
        await _customerService.DidNotReceive().CreateAsync(Arg.Any<CreateCustomerCommand>());
    }

    [Fact]
    public async Task Service_validation_errors_are_applied_to_model_state()
    {
        var controller = CreateController();
        var errors = new List<FieldError>
        {
            new("OwnerId", "Assigned owner must be an active team member."),
            new("Status", "Status is out of range.")
        };
        _customerService.CreateAsync(Arg.Any<CreateCustomerCommand>())
            .Returns(Result<CustomerDetail>.Fail(ErrorKind.Validation, errors));

        var model = new CustomerFormViewModel { Name = "Acme", Email = "acme@example.com", Phone = "9876543210" };
        var result = await controller.Create(model);

        var viewResult = result.ShouldBeOfType<ViewResult>();
        controller.ModelState[nameof(CustomerFormViewModel.OwnerId)]!.Errors[0].ErrorMessage
            .ShouldBe("Assigned owner must be an active team member.");
        controller.ModelState[nameof(CustomerFormViewModel.Status)]!.Errors[0].ErrorMessage
            .ShouldBe("Status is out of range.");
    }
}

using System.Reflection;
using AcxiomCrm.Application.Abstractions;
using AcxiomCrm.Application.Audit;
using AcxiomCrm.Application.Common;
using AcxiomCrm.Application.Customers;
using AcxiomCrm.Application.Users;
using AcxiomCrm.Domain.Security;
using AcxiomCrm.Web.Controllers;
using AcxiomCrm.Web.Services;
using AcxiomCrm.Web.ViewModels.Customers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using Shouldly;

namespace AcxiomCrm.Web.Tests;

public class CustomerAccessTests
{
    private readonly ICustomerService _customerService = Substitute.For<ICustomerService>();
    private readonly IUserDirectory _userDirectory = Substitute.For<IUserDirectory>();
    private readonly IAuditQueryService _auditQueryService = Substitute.For<IAuditQueryService>();
    private readonly ICurrentUser _currentUser = Substitute.For<ICurrentUser>();
    private readonly IFlash _flash = Substitute.For<IFlash>();

    private CustomersController CreateController(string role = Roles.Admin)
    {
        _currentUser.Role.Returns(role);
        return new CustomersController(
            _customerService,
            _userDirectory,
            _auditQueryService,
            _currentUser,
            _flash);
    }

    [Fact]
    public void Controller_and_actions_have_expected_authorize_policies()
    {
        var controllerType = typeof(CustomersController);

        var controllerAuth = controllerType.GetCustomAttribute<AuthorizeAttribute>();
        controllerAuth.ShouldNotBeNull();
        controllerAuth.Policy.ShouldBe(Permissions.CustomersView);

        var createGet = controllerType.GetMethod(nameof(CustomersController.Create), [typeof(CancellationToken)]);
        createGet?.GetCustomAttribute<AuthorizeAttribute>()?.Policy.ShouldBe(Permissions.CustomersCreate);

        var createPost = controllerType.GetMethod(nameof(CustomersController.Create), [typeof(CustomerFormViewModel), typeof(CancellationToken)]);
        createPost?.GetCustomAttribute<AuthorizeAttribute>()?.Policy.ShouldBe(Permissions.CustomersCreate);

        var editGet = controllerType.GetMethod(nameof(CustomersController.Edit), [typeof(int), typeof(CancellationToken)]);
        editGet?.GetCustomAttribute<AuthorizeAttribute>()?.Policy.ShouldBe(Permissions.CustomersEdit);

        var editPost = controllerType.GetMethod(nameof(CustomersController.Edit), [typeof(int), typeof(CustomerFormViewModel), typeof(CancellationToken)]);
        editPost?.GetCustomAttribute<AuthorizeAttribute>()?.Policy.ShouldBe(Permissions.CustomersEdit);

        var deletePost = controllerType.GetMethod(nameof(CustomersController.Delete), [typeof(int), typeof(CancellationToken)]);
        deletePost?.GetCustomAttribute<AuthorizeAttribute>()?.Policy.ShouldBe(Permissions.CustomersDelete);
    }

    [Fact]
    public async Task Other_team_id_returns_NotFound_on_details()
    {
        var controller = CreateController(Roles.SalesExecutive);
        _customerService.GetAsync(999).Returns(Result<CustomerDetail>.Fail(ErrorKind.NotFound));

        var result = await controller.Details(999);

        result.ShouldBeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task Other_team_id_returns_NotFound_on_edit_get()
    {
        var controller = CreateController(Roles.SalesExecutive);
        _customerService.GetAsync(999).Returns(Result<CustomerDetail>.Fail(ErrorKind.NotFound));

        var result = await controller.Edit(999);

        result.ShouldBeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task Other_team_id_returns_NotFound_on_edit_post()
    {
        var controller = CreateController(Roles.SalesExecutive);
        _customerService.UpdateAsync(999, Arg.Any<UpdateCustomerCommand>())
            .Returns(Result<CustomerDetail>.Fail(ErrorKind.NotFound));

        var model = new CustomerFormViewModel { Id = 999, Name = "Test", Email = "t@e.com", Phone = "9876543210" };
        var result = await controller.Edit(999, model);

        result.ShouldBeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task Other_team_id_returns_NotFound_on_delete()
    {
        var controller = CreateController(Roles.Manager);
        _customerService.DeleteAsync(999).Returns(Result.Fail(ErrorKind.NotFound));

        var result = await controller.Delete(999);

        result.ShouldBeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task Unprivileged_action_returns_Forbid_when_service_returns_Forbidden()
    {
        var controller = CreateController(Roles.SalesExecutive);
        _customerService.DeleteAsync(10).Returns(Result.Fail(ErrorKind.Forbidden));

        var result = await controller.Delete(10);

        result.ShouldBeOfType<ForbidResult>();
    }
}

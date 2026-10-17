using System.Reflection;
using AcxiomCrm.Application.Abstractions;
using AcxiomCrm.Application.Audit;
using AcxiomCrm.Application.Common;
using AcxiomCrm.Application.Customers;
using AcxiomCrm.Application.Leads;
using AcxiomCrm.Application.Users;
using AcxiomCrm.Domain.Enums;
using AcxiomCrm.Domain.Security;
using AcxiomCrm.Web.Controllers;
using AcxiomCrm.Web.Services;
using AcxiomCrm.Web.ViewModels.Leads;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using Shouldly;

namespace AcxiomCrm.Web.Tests;

public class LeadAccessTests
{
    private readonly ILeadService _leadService = Substitute.For<ILeadService>();
    private readonly ICustomerService _customerService = Substitute.For<ICustomerService>();
    private readonly IUserDirectory _userDirectory = Substitute.For<IUserDirectory>();
    private readonly IAuditQueryService _auditQueryService = Substitute.For<IAuditQueryService>();
    private readonly IOpportunityFactory _opportunityFactory = Substitute.For<IOpportunityFactory>();
    private readonly IClock _clock = Substitute.For<IClock>();
    private readonly ICurrentUser _currentUser = Substitute.For<ICurrentUser>();
    private readonly IFlash _flash = Substitute.For<IFlash>();

    private LeadsController CreateController(string role = Roles.Admin)
    {
        _currentUser.Role.Returns(role);
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

    [Fact]
    public void Controller_and_actions_have_expected_authorize_policies()
    {
        var controllerType = typeof(LeadsController);

        var controllerAuth = controllerType.GetCustomAttribute<AuthorizeAttribute>();
        controllerAuth.ShouldNotBeNull();
        controllerAuth.Policy.ShouldBe(Permissions.LeadsView);

        var detailsGet = controllerType.GetMethod(nameof(LeadsController.Details), [typeof(int), typeof(CancellationToken)]);
        detailsGet.ShouldNotBeNull();

        var createGet = controllerType.GetMethod(nameof(LeadsController.Create), [typeof(CancellationToken)]);
        createGet?.GetCustomAttribute<AuthorizeAttribute>()?.Policy.ShouldBe(Permissions.LeadsCreate);

        var createPost = controllerType.GetMethod(nameof(LeadsController.Create), [typeof(LeadFormViewModel), typeof(CancellationToken)]);
        createPost?.GetCustomAttribute<AuthorizeAttribute>()?.Policy.ShouldBe(Permissions.LeadsCreate);

        var editGet = controllerType.GetMethod(nameof(LeadsController.Edit), [typeof(int), typeof(CancellationToken)]);
        editGet?.GetCustomAttribute<AuthorizeAttribute>()?.Policy.ShouldBe(Permissions.LeadsEdit);

        var editPost = controllerType.GetMethod(nameof(LeadsController.Edit), [typeof(int), typeof(LeadFormViewModel), typeof(CancellationToken)]);
        editPost?.GetCustomAttribute<AuthorizeAttribute>()?.Policy.ShouldBe(Permissions.LeadsEdit);

        var deletePost = controllerType.GetMethod(nameof(LeadsController.Delete), [typeof(int), typeof(CancellationToken)]);
        deletePost?.GetCustomAttribute<AuthorizeAttribute>()?.Policy.ShouldBe(Permissions.LeadsDelete);

        var changeStatusPost = controllerType.GetMethod(nameof(LeadsController.ChangeStatus), [typeof(int), typeof(ChangeStatusViewModel), typeof(CancellationToken)]);
        changeStatusPost?.GetCustomAttribute<AuthorizeAttribute>()?.Policy.ShouldBe(Permissions.LeadsEdit);

        var assignPost = controllerType.GetMethod(nameof(LeadsController.Assign), [typeof(int), typeof(AssignLeadViewModel), typeof(CancellationToken)]);
        assignPost?.GetCustomAttribute<AuthorizeAttribute>()?.Policy.ShouldBe(Permissions.LeadsAssign);

        var convertGet = controllerType.GetMethod(nameof(LeadsController.Convert), [typeof(int), typeof(CancellationToken)]);
        convertGet?.GetCustomAttribute<AuthorizeAttribute>()?.Policy.ShouldBe(Permissions.LeadsConvert);

        var convertPost = controllerType.GetMethod(nameof(LeadsController.Convert), [typeof(int), typeof(ConvertLeadViewModel), typeof(CancellationToken)]);
        convertPost?.GetCustomAttribute<AuthorizeAttribute>()?.Policy.ShouldBe(Permissions.LeadsConvert);
    }

    [Fact]
    public async Task Other_team_id_returns_NotFound_on_details()
    {
        var controller = CreateController(Roles.SalesExecutive);
        _leadService.GetAsync(999).Returns(Result<LeadDetail>.Fail(ErrorKind.NotFound));

        var result = await controller.Details(999);

        result.ShouldBeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task Other_team_id_returns_NotFound_on_edit_get()
    {
        var controller = CreateController(Roles.SalesExecutive);
        _leadService.GetAsync(999).Returns(Result<LeadDetail>.Fail(ErrorKind.NotFound));

        var result = await controller.Edit(999);

        result.ShouldBeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task Other_team_id_returns_NotFound_on_edit_post()
    {
        var controller = CreateController(Roles.SalesExecutive);
        _leadService.UpdateAsync(999, Arg.Any<UpdateLeadCommand>())
            .Returns(Result<LeadDetail>.Fail(ErrorKind.NotFound));

        var model = new LeadFormViewModel { Id = 999, Name = "Test", Email = "t@e.com", Phone = "9876543210" };
        var result = await controller.Edit(999, model);

        result.ShouldBeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task Other_team_id_returns_NotFound_on_delete()
    {
        var controller = CreateController(Roles.Manager);
        _leadService.DeleteAsync(999).Returns(Result.Fail(ErrorKind.NotFound));

        var result = await controller.Delete(999);

        result.ShouldBeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task Other_team_id_returns_NotFound_on_change_status()
    {
        var controller = CreateController(Roles.Manager);
        _leadService.ChangeStatusAsync(999, LeadStatus.Contacted, Arg.Any<byte[]>())
            .Returns(Result<LeadDetail>.Fail(ErrorKind.NotFound));

        var result = await controller.ChangeStatus(999, new ChangeStatusViewModel { To = LeadStatus.Contacted, RowVersion = [1] });

        result.ShouldBeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task Other_team_id_returns_NotFound_on_assign()
    {
        var controller = CreateController(Roles.Manager);
        _leadService.AssignAsync(999, "user-2", Arg.Any<byte[]>())
            .Returns(Result<LeadDetail>.Fail(ErrorKind.NotFound));

        var result = await controller.Assign(999, new AssignLeadViewModel { OwnerId = "user-2", RowVersion = [1] });

        result.ShouldBeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task Other_team_id_returns_NotFound_on_convert()
    {
        var controller = CreateController(Roles.SalesExecutive);
        _leadService.GetAsync(999).Returns(Result<LeadDetail>.Fail(ErrorKind.NotFound));

        var result = await controller.Convert(999);

        result.ShouldBeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task Assign_post_returns_Forbid_for_executive()
    {
        var controller = CreateController(Roles.SalesExecutive);
        _leadService.AssignAsync(10, "user-2", Arg.Any<byte[]>())
            .Returns(Result<LeadDetail>.Fail(ErrorKind.Forbidden));

        var result = await controller.Assign(10, new AssignLeadViewModel { OwnerId = "user-2", RowVersion = [1] });

        result.ShouldBeOfType<ForbidResult>();
    }
}

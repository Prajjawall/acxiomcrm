using AcxiomCrm.Application.Abstractions;
using AcxiomCrm.Application.Audit;
using AcxiomCrm.Application.Common;
using AcxiomCrm.Application.Customers;
using AcxiomCrm.Application.Leads;
using AcxiomCrm.Application.Users;
using AcxiomCrm.Domain.Enums;
using AcxiomCrm.Domain.Security;
using AcxiomCrm.Domain.Validation;
using AcxiomCrm.Web.Controllers;
using AcxiomCrm.Web.Services;
using AcxiomCrm.Web.ViewModels.Leads;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using Shouldly;

namespace AcxiomCrm.Web.Tests;

public class LeadsControllerTests
{
    private static readonly DateTime FixedDate = new(2026, 10, 17, 10, 0, 0, DateTimeKind.Utc);
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
        _currentUser.IsAuthenticated.Returns(true);
        _currentUser.Role.Returns(role);
        _currentUser.UserId.Returns("user-1");
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
    public async Task Index_lists_only_in_scope_leads_paged()
    {
        var controller = CreateController(Roles.SalesExecutive);
        var items = new List<LeadListItem>
        {
            new(1, "LEAD-000001", "Acme Lead", "Acme Corp", "acme@example.com", "9876543210",
                LeadSource.Website, LeadStatus.New, LeadPriority.High, 50000m, "user-1", "John", FixedDate)
        };
        _leadService.SearchAsync(Arg.Any<LeadFilter>(), Arg.Any<PageQuery>())
            .Returns(new PagedResult<LeadListItem>(items, 1, 20, 1));

        var result = await controller.Index();

        var viewResult = result.ShouldBeOfType<ViewResult>();
        var model = viewResult.Model.ShouldBeOfType<LeadListViewModel>();
        model.Result.Total.ShouldBe(1);
        model.Result.Items.Count.ShouldBe(1);
        model.Result.Items[0].Name.ShouldBe("Acme Lead");
    }

    [Fact]
    public async Task Index_search_and_filter_preserve_query_in_pager()
    {
        var controller = CreateController(Roles.Admin);
        _leadService.SearchAsync(Arg.Any<LeadFilter>(), Arg.Any<PageQuery>())
            .Returns(new PagedResult<LeadListItem>([], 2, 10, 0));

        var result = await controller.Index(
            search: "Acme",
            status: LeadStatus.Qualified,
            priority: LeadPriority.High,
            source: LeadSource.Website,
            ownerId: "user-2",
            page: 2,
            pageSize: 10,
            sort: "ExpectedValue",
            desc: true);

        var viewResult = result.ShouldBeOfType<ViewResult>();
        var model = viewResult.Model.ShouldBeOfType<LeadListViewModel>();
        model.Filter.Search.ShouldBe("Acme");
        model.Filter.Status.ShouldBe(LeadStatus.Qualified);
        model.Filter.Priority.ShouldBe(LeadPriority.High);
        model.Filter.Source.ShouldBe(LeadSource.Website);
        model.Filter.OwnerId.ShouldBe("user-2");
        model.PageQuery.Page.ShouldBe(2);
        model.PageQuery.PageSize.ShouldBe(10);
        model.PageQuery.Sort.ShouldBe("ExpectedValue");
        model.PageQuery.Desc.ShouldBeTrue();
    }

    [Fact]
    public async Task Create_get_for_executive_hides_owner_field()
    {
        var controller = CreateController(Roles.SalesExecutive);

        var result = await controller.Create();

        var viewResult = result.ShouldBeOfType<ViewResult>();
        var model = viewResult.Model.ShouldBeOfType<LeadFormViewModel>();
        model.OwnerOptions.ShouldBeEmpty();
        await _userDirectory.DidNotReceive().ListAssignableOwnersAsync();
    }

    [Fact]
    public async Task Create_get_for_manager_loads_owner_options()
    {
        var controller = CreateController(Roles.Manager);
        var options = new List<UserOption> { new("user-2", "Jane Executive", Roles.SalesExecutive) };
        _userDirectory.ListAssignableOwnersAsync().Returns(options);

        var result = await controller.Create();

        var viewResult = result.ShouldBeOfType<ViewResult>();
        var model = viewResult.Model.ShouldBeOfType<LeadFormViewModel>();
        model.OwnerOptions.Count.ShouldBe(1);
        model.OwnerOptions[0].Name.ShouldBe("Jane Executive");
    }

    [Fact]
    public async Task Create_post_valid_redirects_to_details_with_flash()
    {
        var controller = CreateController(Roles.Admin);
        var detail = new LeadDetail(
            1, "LEAD-000001", "New Lead", "Corp", "lead@example.com", "9876543210",
            LeadSource.Website, LeadStatus.New, LeadPriority.Medium, 1000m, "user-1", "Admin",
            null, null, null, null, null, FixedDate, null, [1], [LeadStatus.Contacted]);

        _leadService.CreateAsync(Arg.Any<CreateLeadCommand>())
            .Returns(Result<LeadDetail>.Ok(detail));

        var model = new LeadFormViewModel
        {
            Name = "New Lead",
            Email = "lead@example.com",
            Phone = "9876543210",
            Source = LeadSource.Website,
            Priority = LeadPriority.Medium,
            ExpectedValue = 1000m
        };

        var result = await controller.Create(model);

        var redirect = result.ShouldBeOfType<RedirectToActionResult>();
        redirect.ActionName.ShouldBe(nameof(LeadsController.Details));
        redirect.RouteValues!["id"].ShouldBe(1);
        _flash.Received(1).Success("Lead created.");
    }

    [Fact]
    public async Task Edit_post_ignores_crafted_status_field()
    {
        var controller = CreateController(Roles.Admin);
        UpdateLeadCommand? capturedCommand = null;

        _leadService.UpdateAsync(1, Arg.Do<UpdateLeadCommand>(c => capturedCommand = c))
            .Returns(Result<LeadDetail>.Ok(new LeadDetail(
                1, "LEAD-000001", "Updated Name", null, "lead@example.com", "9876543210",
                LeadSource.Website, LeadStatus.New, LeadPriority.Low, 500m, "user-1", "Admin",
                null, null, null, null, null, FixedDate, null, [1], [])));

        var model = new LeadFormViewModel
        {
            Id = 1,
            Name = "Updated Name",
            Email = "lead@example.com",
            Phone = "9876543210",
            Source = LeadSource.Website,
            Priority = LeadPriority.Low,
            ExpectedValue = 500m,
            RowVersion = Convert.ToBase64String(new byte[] { 1 })
        };

        var result = await controller.Edit(1, model);

        result.ShouldBeOfType<RedirectToActionResult>();
        capturedCommand.ShouldNotBeNull();
        capturedCommand.Name.ShouldBe("Updated Name");
        // UpdateLeadCommand has no Status property at all!
    }

    [Fact]
    public async Task Details_shows_status_buttons_only_for_allowed_transitions()
    {
        var controller = CreateController(Roles.SalesExecutive);
        var detail = new LeadDetail(
            1, "LEAD-000001", "Qualified Lead", null, "lead@example.com", "9876543210",
            LeadSource.Website, LeadStatus.Qualified, LeadPriority.High, 20000m, "user-1", "Executive",
            null, null, null, null, null, FixedDate, null, [1], [LeadStatus.Unqualified, LeadStatus.Lost]);

        _leadService.GetAsync(1).Returns(Result<LeadDetail>.Ok(detail));
        _auditQueryService.ForRecordAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<PageQuery>())
            .Returns(new PagedResult<AuditListItem>([], 1, 50, 0));

        var result = await controller.Details(1);

        var viewResult = result.ShouldBeOfType<ViewResult>();
        var model = viewResult.Model.ShouldBeOfType<LeadDetailsViewModel>();
        model.Lead.AllowedTransitions.ShouldBe([LeadStatus.Unqualified, LeadStatus.Lost]);
        model.CanConvert.ShouldBeTrue();
    }

    [Fact]
    public async Task ChangeStatus_valid_redirects_with_flash()
    {
        var controller = CreateController(Roles.SalesExecutive);
        var updated = new LeadDetail(
            1, "LEAD-000001", "Lead", null, "lead@example.com", "9876543210",
            LeadSource.Website, LeadStatus.Contacted, LeadPriority.Medium, 1000m, "user-1", "Executive",
            null, null, null, null, null, FixedDate, null, [2], []);

        _leadService.ChangeStatusAsync(1, LeadStatus.Contacted, Arg.Any<byte[]>())
            .Returns(Result<LeadDetail>.Ok(updated));

        var result = await controller.ChangeStatus(1, new ChangeStatusViewModel { To = LeadStatus.Contacted, RowVersion = [1] });

        var redirect = result.ShouldBeOfType<RedirectToActionResult>();
        redirect.ActionName.ShouldBe(nameof(LeadsController.Details));
        _flash.Received(1).Success("Lead status updated to Contacted.");
    }

    [Fact]
    public async Task ChangeStatus_with_illegal_target_sets_flash_error_and_redirects()
    {
        var controller = CreateController(Roles.SalesExecutive);
        _leadService.ChangeStatusAsync(1, LeadStatus.Converted, Arg.Any<byte[]>())
            .Returns(Result<LeadDetail>.Fail(ErrorKind.Conflict, new FieldError("Status", "A lead cannot move from New to Converted.")));

        var result = await controller.ChangeStatus(1, new ChangeStatusViewModel { To = LeadStatus.Converted, RowVersion = [1] });

        var redirect = result.ShouldBeOfType<RedirectToActionResult>();
        redirect.ActionName.ShouldBe(nameof(LeadsController.Details));
        _flash.Received(1).Error("A lead cannot move from New to Converted.");
    }

    [Fact]
    public async Task Assign_for_manager_reassigns_lead_and_sets_flash()
    {
        var controller = CreateController(Roles.Manager);
        var updated = new LeadDetail(
            1, "LEAD-000001", "Lead", null, "lead@example.com", "9876543210",
            LeadSource.Website, LeadStatus.New, LeadPriority.Medium, 1000m, "user-2", "Jane",
            null, null, null, null, null, FixedDate, null, [2], []);

        _leadService.AssignAsync(1, "user-2", Arg.Any<byte[]>())
            .Returns(Result<LeadDetail>.Ok(updated));

        var result = await controller.Assign(1, new AssignLeadViewModel { OwnerId = "user-2", RowVersion = [1] });

        var redirect = result.ShouldBeOfType<RedirectToActionResult>();
        redirect.ActionName.ShouldBe(nameof(LeadsController.Details));
        _flash.Received(1).Success("Lead assigned to Jane.");
    }
}

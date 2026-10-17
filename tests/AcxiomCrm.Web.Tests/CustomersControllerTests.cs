using AcxiomCrm.Application.Abstractions;
using AcxiomCrm.Application.Audit;
using AcxiomCrm.Application.Common;
using AcxiomCrm.Application.Customers;
using AcxiomCrm.Application.Users;
using AcxiomCrm.Domain.Enums;
using AcxiomCrm.Domain.Security;
using AcxiomCrm.Domain.Validation;
using AcxiomCrm.Web.Controllers;
using AcxiomCrm.Web.Services;
using AcxiomCrm.Web.ViewModels.Customers;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using Shouldly;

namespace AcxiomCrm.Web.Tests;

public class CustomersControllerTests
{
    private static readonly DateTime FixedDate = new(2026, 10, 17, 10, 0, 0, DateTimeKind.Utc);
    private readonly ICustomerService _customerService = Substitute.For<ICustomerService>();
    private readonly IUserDirectory _userDirectory = Substitute.For<IUserDirectory>();
    private readonly IAuditQueryService _auditQueryService = Substitute.For<IAuditQueryService>();
    private readonly ICurrentUser _currentUser = Substitute.For<ICurrentUser>();
    private readonly IFlash _flash = Substitute.For<IFlash>();

    private CustomersController CreateController(string role = Roles.Admin)
    {
        _currentUser.Role.Returns(role);
        _currentUser.UserId.Returns("user-1");
        return new CustomersController(
            _customerService,
            _userDirectory,
            _auditQueryService,
            _currentUser,
            _flash);
    }

    [Fact]
    public async Task Index_lists_only_in_scope_customers_paged()
    {
        var controller = CreateController(Roles.SalesExecutive);
        var items = new List<CustomerListItem>
        {
            new(1, "CUST-000001", "Acme", null, "acme@example.com", "9876543210", CustomerStatus.Active, "user-1", "John", FixedDate)
        };
        _customerService.SearchAsync(Arg.Any<CustomerFilter>(), Arg.Any<PageQuery>())
            .Returns(new PagedResult<CustomerListItem>(items, 1, 20, 1));

        var result = await controller.Index();

        var viewResult = result.ShouldBeOfType<ViewResult>();
        var model = viewResult.Model.ShouldBeOfType<CustomerListViewModel>();
        model.Result.Total.ShouldBe(1);
        model.Result.Items.Count.ShouldBe(1);
        model.Result.Items[0].Name.ShouldBe("Acme");
    }

    [Fact]
    public async Task Index_search_and_filter_preserve_query_in_pager()
    {
        var controller = CreateController(Roles.Admin);
        _customerService.SearchAsync(Arg.Any<CustomerFilter>(), Arg.Any<PageQuery>())
            .Returns(new PagedResult<CustomerListItem>([], 2, 10, 0));

        var result = await controller.Index(
            search: "Acme",
            status: CustomerStatus.Inactive,
            ownerId: "user-2",
            page: 2,
            pageSize: 10,
            sort: "Name",
            desc: true);

        var viewResult = result.ShouldBeOfType<ViewResult>();
        var model = viewResult.Model.ShouldBeOfType<CustomerListViewModel>();
        model.Filter.Search.ShouldBe("Acme");
        model.Filter.Status.ShouldBe(CustomerStatus.Inactive);
        model.Filter.OwnerId.ShouldBe("user-2");
        model.PageQuery.Page.ShouldBe(2);
        model.PageQuery.PageSize.ShouldBe(10);
        model.PageQuery.Sort.ShouldBe("Name");
        model.PageQuery.Desc.ShouldBeTrue();
    }

    [Fact]
    public async Task Create_get_for_executive_hides_owner_field()
    {
        var controller = CreateController(Roles.SalesExecutive);

        var result = await controller.Create();

        var viewResult = result.ShouldBeOfType<ViewResult>();
        var model = viewResult.Model.ShouldBeOfType<CustomerFormViewModel>();
        model.OwnerOptions.ShouldBeEmpty();
        await _userDirectory.DidNotReceive().ListAssignableOwnersAsync();
    }

    [Fact]
    public async Task Create_get_for_manager_loads_owner_options()
    {
        var controller = CreateController(Roles.Manager);
        var owners = new List<UserOption> { new("exec-1", "Exec One", Roles.SalesExecutive) };
        _userDirectory.ListAssignableOwnersAsync().Returns(owners);

        var result = await controller.Create();

        var viewResult = result.ShouldBeOfType<ViewResult>();
        var model = viewResult.Model.ShouldBeOfType<CustomerFormViewModel>();
        model.OwnerOptions.Count.ShouldBe(1);
        model.OwnerOptions[0].Name.ShouldBe("Exec One");
    }

    [Fact]
    public async Task Create_post_valid_redirects_to_details_with_flash()
    {
        var controller = CreateController(Roles.Admin);
        var created = new CustomerDetail(
            15, "CUST-000015", "Omega", null, "omega@example.com", "9876543210",
            null, null, null, CustomerStatus.Active, "user-1", "Admin", null,
            FixedDate, null, [1, 2, 3]);

        _customerService.CreateAsync(Arg.Any<CreateCustomerCommand>())
            .Returns(Result<CustomerDetail>.Ok(created));

        var model = new CustomerFormViewModel
        {
            Name = "Omega",
            Email = "omega@example.com",
            Phone = "9876543210"
        };

        var result = await controller.Create(model);

        var redirect = result.ShouldBeOfType<RedirectToActionResult>();
        redirect.ActionName.ShouldBe(nameof(CustomersController.Details));
        redirect.RouteValues!["id"].ShouldBe(15);
        _flash.Received(1).Success("Customer created.");
    }

    [Fact]
    public async Task Create_post_invalid_redisplays_with_field_errors_and_data_val_attributes()
    {
        var controller = CreateController(Roles.Admin);
        _customerService.CreateAsync(Arg.Any<CreateCustomerCommand>())
            .Returns(Result<CustomerDetail>.Fail(ErrorKind.Validation, [new FieldError("Name", "Customer name is required.")]));

        var model = new CustomerFormViewModel();

        var result = await controller.Create(model);

        var viewResult = result.ShouldBeOfType<ViewResult>();
        controller.ModelState[nameof(CustomerFormViewModel.Name)]!.Errors[0].ErrorMessage
            .ShouldBe("Customer name is required.");
        viewResult.Model.ShouldBe(model);
    }

    [Fact]
    public async Task Duplicate_email_shows_message_on_email_field()
    {
        var controller = CreateController(Roles.Admin);
        _customerService.CreateAsync(Arg.Any<CreateCustomerCommand>())
            .Returns(Result<CustomerDetail>.Fail(ErrorKind.Conflict, [new FieldError("Email", "A customer with this email already exists.")]));

        var model = new CustomerFormViewModel { Name = "Acme", Email = "dup@example.com", Phone = "9876543210" };

        var result = await controller.Create(model);

        var viewResult = result.ShouldBeOfType<ViewResult>();
        controller.ModelState[nameof(CustomerFormViewModel.Email)]!.Errors[0].ErrorMessage
            .ShouldBe("A customer with this email already exists.");
    }

    [Fact]
    public async Task Edit_post_stale_rowversion_shows_conflict_banner_with_reload_link()
    {
        var controller = CreateController(Roles.Admin);
        _customerService.UpdateAsync(42, Arg.Any<UpdateCustomerCommand>())
            .Returns(Result<CustomerDetail>.Fail(ErrorKind.Conflict, [new FieldError("", "Record was modified by another user. Please reload.")]));

        var model = new CustomerFormViewModel
        {
            Id = 42,
            Name = "Acme",
            Email = "acme@example.com",
            Phone = "9876543210",
            RowVersion = Convert.ToBase64String([1, 2, 3])
        };

        var result = await controller.Edit(42, model);

        var viewResult = result.ShouldBeOfType<ViewResult>();
        controller.ModelState[""]!.Errors[0].ErrorMessage
            .ShouldBe("Record was modified by another user. Please reload.");
    }

    [Fact]
    public async Task Delete_post_redirects_with_flash_and_requires_antiforgery()
    {
        var controller = CreateController(Roles.Admin);
        _customerService.DeleteAsync(10).Returns(Result.Ok());

        var result = await controller.Delete(10);

        var redirect = result.ShouldBeOfType<RedirectToActionResult>();
        redirect.ActionName.ShouldBe(nameof(CustomersController.Index));
        _flash.Received(1).Success("Customer deactivated.");
    }

    [Fact]
    public async Task Details_history_tab_lists_audit_entries_newest_first()
    {
        var controller = CreateController(Roles.Admin);
        var detail = new CustomerDetail(
            10, "CUST-000010", "Beta", null, "beta@example.com", "9123456780",
            null, null, null, CustomerStatus.Active, "user-1", "Admin", null,
            FixedDate, null, [1, 2, 3]);

        _customerService.GetAsync(10).Returns(Result<CustomerDetail>.Ok(detail));

        var auditItems = new List<AuditListItem>
        {
            new(2, FixedDate.AddMinutes(5), "admin", "Customers", "Update", "Customer", "10", AuditResult.Success, "127.0.0.1", null),
            new(1, FixedDate, "admin", "Customers", "Create", "Customer", "10", AuditResult.Success, "127.0.0.1", null)
        };
        _auditQueryService.ForRecordAsync("Customer", "10", Arg.Any<PageQuery>())
            .Returns(new PagedResult<AuditListItem>(auditItems, 1, 50, 2));

        var result = await controller.Details(10);

        var viewResult = result.ShouldBeOfType<ViewResult>();
        var model = viewResult.Model.ShouldBeOfType<CustomerDetailsViewModel>();
        model.Customer.Id.ShouldBe(10);
        model.AuditHistory.Count.ShouldBe(2);
        model.AuditHistory[0].Action.ShouldBe("Update");
        model.AuditHistory[1].Action.ShouldBe("Create");
    }
}

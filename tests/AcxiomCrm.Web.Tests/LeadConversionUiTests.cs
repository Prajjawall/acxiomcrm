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

public class LeadConversionUiTests
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

    private LeadsController CreateLeadsController(string role = Roles.SalesExecutive)
    {
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

    private CustomersController CreateCustomersController(string role = Roles.SalesExecutive)
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
    public async Task Convert_get_loads_lead_details_and_suggested_duplicate_if_in_scope()
    {
        var controller = CreateLeadsController();
        var lead = new LeadDetail(
            1, "LEAD-000001", "Qualified Acme", "Acme Corp", "acme@example.com", "9876543210",
            LeadSource.Website, LeadStatus.Qualified, LeadPriority.High, 25000m, "user-1", "Executive",
            null, null, null, null, null, FixedDate, null, [1], []);

        _leadService.GetAsync(1).Returns(Result<LeadDetail>.Ok(lead));
        _leadService.FindConvertibleDuplicateCustomerIdAsync(1).Returns(42);
        _customerService.GetAsync(42).Returns(Result<CustomerDetail>.Ok(new CustomerDetail(
            42, "CUST-000042", "Existing Acme", "Acme Corp", "acme@example.com", "9876543210",
            null, null, null, CustomerStatus.Active, "user-1", "Executive", null, FixedDate, null, [1])));

        var result = await controller.Convert(1);

        var viewResult = result.ShouldBeOfType<ViewResult>();
        var model = viewResult.Model.ShouldBeOfType<ConvertLeadViewModel>();
        model.LeadId.ShouldBe(1);
        model.LeadCode.ShouldBe("LEAD-000001");
        model.LeadName.ShouldBe("Qualified Acme");
        model.SuggestedCustomerId.ShouldBe(42);
        model.SuggestedCustomerLabel.ShouldNotBeNull();
        model.SuggestedCustomerLabel.ShouldContain("CUST-000042");
    }

    [Fact]
    public async Task Convert_post_create_new_customer_happy_path_redirects_to_customer_details_with_flash()
    {
        var controller = CreateLeadsController();
        var lead = new LeadDetail(
            1, "LEAD-000001", "Qualified Acme", "Acme Corp", "acme@example.com", "9876543210",
            LeadSource.Website, LeadStatus.Qualified, LeadPriority.High, 25000m, "user-1", "Executive",
            null, null, null, null, null, FixedDate, null, [1], []);

        _leadService.GetAsync(1).Returns(Result<LeadDetail>.Ok(lead));
        _leadService.ConvertAsync(1, Arg.Any<ConvertLeadCommand>())
            .Returns(Result<ConvertLeadResult>.Ok(new ConvertLeadResult(1, 10, null)));

        _customerService.GetAsync(10).Returns(Result<CustomerDetail>.Ok(new CustomerDetail(
            10, "CUST-000010", "Qualified Acme", "Acme Corp", "acme@example.com", "9876543210",
            null, null, null, CustomerStatus.Active, "user-1", "Executive", null, FixedDate, null, [1])));

        var model = new ConvertLeadViewModel
        {
            LeadId = 1,
            LeadCode = "LEAD-000001",
            ExistingCustomerId = null,
            CreateOpportunity = false,
            RowVersion = [1]
        };

        var result = await controller.Convert(1, model);

        var redirect = result.ShouldBeOfType<RedirectToActionResult>();
        redirect.ControllerName.ShouldBe("Customers");
        redirect.ActionName.ShouldBe("Details");
        redirect.RouteValues!["id"].ShouldBe(10);
        _flash.Received(1).Success("Lead converted. Customer CUST-000010 created.");
    }

    [Fact]
    public async Task Convert_post_link_existing_customer_happy_path_redirects_to_customer_details_with_flash()
    {
        var controller = CreateLeadsController();
        var lead = new LeadDetail(
            1, "LEAD-000001", "Qualified Acme", "Acme Corp", "acme@example.com", "9876543210",
            LeadSource.Website, LeadStatus.Qualified, LeadPriority.High, 25000m, "user-1", "Executive",
            null, null, null, null, null, FixedDate, null, [1], []);

        _leadService.GetAsync(1).Returns(Result<LeadDetail>.Ok(lead));
        _leadService.ConvertAsync(1, Arg.Any<ConvertLeadCommand>())
            .Returns(Result<ConvertLeadResult>.Ok(new ConvertLeadResult(1, 42, null)));

        _customerService.GetAsync(42).Returns(Result<CustomerDetail>.Ok(new CustomerDetail(
            42, "CUST-000042", "Existing Acme", "Acme Corp", "acme@example.com", "9876543210",
            null, null, null, CustomerStatus.Active, "user-1", "Executive", null, FixedDate, null, [1])));

        var model = new ConvertLeadViewModel
        {
            LeadId = 1,
            LeadCode = "LEAD-000001",
            ExistingCustomerId = 42,
            CreateOpportunity = false,
            RowVersion = [1]
        };

        var result = await controller.Convert(1, model);

        var redirect = result.ShouldBeOfType<RedirectToActionResult>();
        redirect.ControllerName.ShouldBe("Customers");
        redirect.ActionName.ShouldBe("Details");
        redirect.RouteValues!["id"].ShouldBe(42);
        _flash.Received(1).Success("Lead converted. Linked to customer CUST-000042.");
    }

    [Fact]
    public async Task Convert_post_duplicate_customer_shows_link_existing_suggestion()
    {
        var controller = CreateLeadsController();
        var lead = new LeadDetail(
            1, "LEAD-000001", "Qualified Acme", "Acme Corp", "acme@example.com", "9876543210",
            LeadSource.Website, LeadStatus.Qualified, LeadPriority.High, 25000m, "user-1", "Executive",
            null, null, null, null, null, FixedDate, null, [1], []);

        _leadService.GetAsync(1).Returns(Result<LeadDetail>.Ok(lead));
        _leadService.ConvertAsync(1, Arg.Any<ConvertLeadCommand>())
            .Returns(Result<ConvertLeadResult>.Fail(ErrorKind.Conflict,
                new FieldError("Email", "A customer with this email already exists. Link to the existing customer instead.")));

        _leadService.FindConvertibleDuplicateCustomerIdAsync(1).Returns(42);
        _customerService.GetAsync(42).Returns(Result<CustomerDetail>.Ok(new CustomerDetail(
            42, "CUST-000042", "Existing Acme", "Acme Corp", "acme@example.com", "9876543210",
            null, null, null, CustomerStatus.Active, "user-1", "Executive", null, FixedDate, null, [1])));

        var model = new ConvertLeadViewModel
        {
            LeadId = 1,
            LeadCode = "LEAD-000001",
            ExistingCustomerId = null,
            CreateOpportunity = false,
            RowVersion = [1]
        };

        var result = await controller.Convert(1, model);

        var viewResult = result.ShouldBeOfType<ViewResult>();
        var viewModel = viewResult.Model.ShouldBeOfType<ConvertLeadViewModel>();
        viewModel.SuggestedCustomerId.ShouldBe(42);
        viewModel.SuggestedCustomerLabel.ShouldNotBeNull();
        viewModel.SuggestedCustomerLabel.ShouldContain("CUST-000042");
        controller.ModelState["Email"]!.Errors[0].ErrorMessage
            .ShouldBe("A customer with this email already exists. Link to the existing customer instead.");
    }

    [Fact]
    public async Task Convert_post_when_lead_not_qualified_shows_error()
    {
        var controller = CreateLeadsController();
        var lead = new LeadDetail(
            1, "LEAD-000001", "New Lead", "Acme Corp", "acme@example.com", "9876543210",
            LeadSource.Website, LeadStatus.New, LeadPriority.High, 25000m, "user-1", "Executive",
            null, null, null, null, null, FixedDate, null, [1], []);

        _leadService.GetAsync(1).Returns(Result<LeadDetail>.Ok(lead));
        _leadService.ConvertAsync(1, Arg.Any<ConvertLeadCommand>())
            .Returns(Result<ConvertLeadResult>.Fail(ErrorKind.Conflict,
                new FieldError("Status", "Only qualified leads can be converted.")));

        var model = new ConvertLeadViewModel
        {
            LeadId = 1,
            LeadCode = "LEAD-000001",
            ExistingCustomerId = null,
            CreateOpportunity = false,
            RowVersion = [1]
        };

        var result = await controller.Convert(1, model);

        var viewResult = result.ShouldBeOfType<ViewResult>();
        controller.ModelState["Status"]!.Errors[0].ErrorMessage
            .ShouldBe("Only qualified leads can be converted.");
    }

    [Fact]
    public async Task Lookup_endpoint_returns_only_in_scope_active_customers_with_json_and_no_extra_fields()
    {
        var controller = CreateCustomersController();
        var items = new List<CustomerLookupItem>
        {
            new(10, "CUST-000010", "Acme Active", "Acme Corp")
        };
        _customerService.LookupAsync("Acme", 20).Returns(items);

        var result = await controller.Lookup("Acme");

        var jsonResult = result.ShouldBeOfType<JsonResult>();
        var value = jsonResult.Value as IEnumerable<object>;
        value.ShouldNotBeNull();
        var first = value.First();
        var props = first.GetType().GetProperties();
        props.Length.ShouldBe(2);
        props.Select(p => p.Name).ShouldBe(["id", "label"], ignoreOrder: true);
    }
}

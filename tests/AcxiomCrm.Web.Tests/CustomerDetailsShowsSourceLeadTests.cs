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
using AcxiomCrm.Web.ViewModels.Customers;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using Shouldly;

namespace AcxiomCrm.Web.Tests;

public class CustomerDetailsShowsSourceLeadTests
{
    private static readonly DateTime FixedDate = new(2026, 10, 17, 10, 0, 0, DateTimeKind.Utc);
    private readonly ICustomerService _customerService = Substitute.For<ICustomerService>();
    private readonly IUserDirectory _userDirectory = Substitute.For<IUserDirectory>();
    private readonly IAuditQueryService _auditQueryService = Substitute.For<IAuditQueryService>();
    private readonly ICurrentUser _currentUser = Substitute.For<ICurrentUser>();
    private readonly IFlash _flash = Substitute.For<IFlash>();
    private readonly ILeadService _leadService = Substitute.For<ILeadService>();

    private CustomersController CreateController(string role = Roles.Admin)
    {
        _currentUser.Role.Returns(role);
        _currentUser.UserId.Returns("user-1");
        return new CustomersController(
            _customerService,
            _userDirectory,
            _auditQueryService,
            _currentUser,
            _flash,
            _leadService);
    }

    [Fact]
    public async Task Details_loads_and_displays_converted_source_leads_in_viewmodel()
    {
        var controller = CreateController();
        var customerDetail = new CustomerDetail(
            1, "CUST-000001", "Kiran Textiles", null, "kiran@example.com", "9876543210",
            null, null, null, CustomerStatus.Active, "user-1", "Admin", null,
            FixedDate, null, [1]);

        _customerService.GetAsync(1).Returns(Result<CustomerDetail>.Ok(customerDetail));
        _auditQueryService.ForRecordAsync("Customer", "1", Arg.Any<PageQuery>())
            .Returns(new PagedResult<AuditListItem>([], 1, 50, 0));

        var sourceLead = new LeadListItem(
            42, "LEAD-000042", "Kiran Contact", "Kiran Textiles", "kiran@example.com", "9876543210",
            LeadSource.Website, LeadStatus.Converted, LeadPriority.High, 75000m, "user-1", "Admin", FixedDate);

        _leadService.SearchAsync(Arg.Is<LeadFilter>(f => f.ConvertedCustomerId == 1), Arg.Any<PageQuery>())
            .Returns(new PagedResult<LeadListItem>([sourceLead], 1, 50, 1));

        var result = await controller.Details(1);

        var viewResult = result.ShouldBeOfType<ViewResult>();
        var model = viewResult.Model.ShouldBeOfType<CustomerDetailsViewModel>();
        model.SourceLeads.Count.ShouldBe(1);
        model.SourceLeads[0].Code.ShouldBe("LEAD-000042");
        model.SourceLeads[0].Name.ShouldBe("Kiran Contact");
    }
}

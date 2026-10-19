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
using AcxiomCrm.Web.ModelBinding;
using AcxiomCrm.Web.Services;
using AcxiomCrm.Web.ViewModels.Opportunities;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using NSubstitute;
using Shouldly;

namespace AcxiomCrm.Web.Tests;

public class OpportunitiesControllerTests
{
    private static readonly DateTime FixedDate = new(2026, 10, 18, 10, 0, 0, DateTimeKind.Utc);
    private static readonly DateOnly Today = new(2026, 10, 18);

    private readonly IOpportunityService _opportunityService = Substitute.For<IOpportunityService>();
    private readonly ICustomerService _customerService = Substitute.For<ICustomerService>();
    private readonly IUserDirectory _userDirectory = Substitute.For<IUserDirectory>();
    private readonly IAuditQueryService _auditQueryService = Substitute.For<IAuditQueryService>();
    private readonly IClock _clock = Substitute.For<IClock>();
    private readonly ICurrentUser _currentUser = Substitute.For<ICurrentUser>();
    private readonly IFlash _flash = Substitute.For<IFlash>();

    public OpportunitiesControllerTests()
    {
        _clock.Today.Returns(Today);
        _clock.UtcNow.Returns(FixedDate);
    }

    private OpportunitiesController CreateController(string role = Roles.Admin, string userId = "user-1")
    {
        _currentUser.IsAuthenticated.Returns(true);
        _currentUser.Role.Returns(role);
        _currentUser.UserId.Returns(userId);

        var controller = new OpportunitiesController(
            _opportunityService,
            _customerService,
            _userDirectory,
            _auditQueryService,
            _clock,
            _currentUser,
            _flash);

        var httpContext = new DefaultHttpContext();
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = httpContext
        };
        controller.TempData = new TempDataDictionary(httpContext, Substitute.For<ITempDataProvider>());
        return controller;
    }

    [Fact]
    public async Task Index_shows_table_with_paged_opportunities()
    {
        var controller = CreateController(Roles.SalesExecutive);
        var items = new List<OpportunityListItem>
        {
            new(1, "Big Deal", 10, "Acme Corp", "CUST-0001", 100000m, 50, 50000m,
                OpportunityStage.Proposal, OpportunityStatus.Open, Today.AddDays(15), false, "user-1", "John")
        };
        _opportunityService.SearchAsync(Arg.Any<OpportunityFilter>(), Arg.Any<PageQuery>())
            .Returns(new PagedResult<OpportunityListItem>(items, 1, 20, 1));

        var result = await controller.Index();

        var viewResult = result.ShouldBeOfType<ViewResult>();
        var model = viewResult.Model.ShouldBeOfType<OpportunityListViewModel>();
        model.Result.Total.ShouldBe(1);
        model.Result.Items.Count.ShouldBe(1);
        model.Result.Items[0].Name.ShouldBe("Big Deal");
    }

    [Fact]
    public async Task Details_shows_opportunity_facts_and_history()
    {
        var controller = CreateController(Roles.Admin);
        var detail = new OpportunityDetail(
            1, "Enterprise Deal", 10, "Acme Corp", "CUST-0001", null,
            500000m, 75, 375000m, OpportunityStage.Negotiation, OpportunityStatus.Open,
            Today.AddDays(10), null, null, LeadSource.Website, "user-1", "John", "Negotiating terms",
            FixedDate, null, [1, 2, 3]);

        _opportunityService.GetAsync(1).Returns(Result<OpportunityDetail>.Ok(detail));
        _auditQueryService.ForRecordAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<PageQuery>(), Arg.Any<CancellationToken>())
            .Returns(new PagedResult<AuditListItem>([], 1, 50, 0));

        var result = await controller.Details(1);

        var viewResult = result.ShouldBeOfType<ViewResult>();
        var model = viewResult.Model.ShouldBeOfType<OpportunityDetailsViewModel>();
        model.Detail.Id.ShouldBe(1);
        model.Detail.Name.ShouldBe("Enterprise Deal");
        model.Detail.WeightedAmount.ShouldBe(375000m);
    }

    [Fact]
    public async Task Details_returns_NotFound_when_missing()
    {
        var controller = CreateController(Roles.Admin);
        _opportunityService.GetAsync(999).Returns(Result<OpportunityDetail>.Fail(ErrorKind.NotFound));

        var result = await controller.Details(999);
        result.ShouldBeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task Create_get_preselects_customer_when_provided()
    {
        var controller = CreateController(Roles.Admin);
        var customer = new CustomerDetail(10, "CUST-0010", "Target Corp", "Target", "cust@example.com", "9876543210",
            null, null, null, CustomerStatus.Active, "user-1", "John", null, FixedDate, null, [1]);
        _customerService.GetAsync(10).Returns(Result<CustomerDetail>.Ok(customer));

        var result = await controller.Create(customerId: 10);

        var viewResult = result.ShouldBeOfType<ViewResult>();
        var model = viewResult.Model.ShouldBeOfType<OpportunityFormViewModel>();
        model.CustomerId.ShouldBe(10);
        model.CustomerName.ShouldBe("Target Corp");
    }

    [Fact]
    public async Task Create_post_valid_redirects_to_details_with_flash()
    {
        var controller = CreateController(Roles.Admin);
        var created = new OpportunityDetail(1, "New Opp", 10, "Acme Corp", "CUST-0001", null,
            25000m, 20, 5000m, OpportunityStage.Qualification, OpportunityStatus.Open,
            Today.AddDays(30), null, null, null, "user-1", "John", null, FixedDate, null, [1]);

        _opportunityService.CreateAsync(Arg.Any<CreateOpportunityCommand>())
            .Returns(Result<OpportunityDetail>.Ok(created));

        var form = new OpportunityFormViewModel
        {
            Name = "New Opp",
            CustomerId = 10,
            Amount = 25000m,
            Probability = 20,
            ExpectedCloseDate = Today.AddDays(30),
            Stage = OpportunityStage.Qualification
        };

        var result = await controller.Create(form);

        var redirect = result.ShouldBeOfType<RedirectToActionResult>();
        redirect.ActionName.ShouldBe(nameof(OpportunitiesController.Details));
        redirect.RouteValues!["id"].ShouldBe(1);
        _flash.Received().Success(Arg.Any<string>());
    }

    [Fact]
    public async Task Create_post_invalid_redisplays_form_with_errors()
    {
        var controller = CreateController(Roles.Admin);
        _opportunityService.CreateAsync(Arg.Any<CreateOpportunityCommand>())
            .Returns(Result<OpportunityDetail>.Fail(ErrorKind.Validation, [new FieldError("CustomerId", "Choose an active customer.")]));

        var form = new OpportunityFormViewModel
        {
            Name = "New Opp",
            CustomerId = 10,
            Amount = 25000m,
            Probability = 20,
            ExpectedCloseDate = Today.AddDays(30),
            Stage = OpportunityStage.Qualification
        };

        var result = await controller.Create(form);

        var viewResult = result.ShouldBeOfType<ViewResult>();
        viewResult.Model.ShouldBe(form);
        controller.ModelState[nameof(OpportunityFormViewModel.CustomerId)]!.Errors[0].ErrorMessage.ShouldBe("Choose an active customer.");
    }

    [Fact]
    public async Task Edit_get_for_closed_opportunity_redirects_to_details_with_warning()
    {
        var controller = CreateController(Roles.Admin);
        var detail = new OpportunityDetail(
            1, "Won Deal", 10, "Acme", "CUST-0001", null,
            50000m, 100, 50000m, OpportunityStage.Won, OpportunityStatus.Won,
            Today, Today, "Contract signed", null, "user-1", "John", null, FixedDate, null, [1]);

        _opportunityService.GetAsync(1).Returns(Result<OpportunityDetail>.Ok(detail));

        var result = await controller.Edit(1);

        var redirect = result.ShouldBeOfType<RedirectToActionResult>();
        redirect.ActionName.ShouldBe(nameof(OpportunitiesController.Details));
        _flash.Received().Warning(Arg.Is<string>(s => s.Contains("closed")));
    }

    [Fact]
    public async Task ChangeStage_to_Won_without_note_redisplays_with_error()
    {
        var controller = CreateController(Roles.Admin);
        _opportunityService.ChangeStageAsync(1, OpportunityStage.Won, null, Arg.Any<byte[]>())
            .Returns(Result<OpportunityDetail>.Fail(ErrorKind.Validation, [new FieldError("CloseNote", "Enter a note about why this opportunity was won.")]));

        var detail = new OpportunityDetail(
            1, "Deal", 10, "Acme", "CUST-0001", null,
            50000m, 50, 25000m, OpportunityStage.Proposal, OpportunityStatus.Open,
            Today.AddDays(10), null, null, null, "user-1", "John", null, FixedDate, null, [1]);
        _opportunityService.GetAsync(1).Returns(Result<OpportunityDetail>.Ok(detail));

        var changeModel = new ChangeStageViewModel
        {
            TargetStage = OpportunityStage.Won,
            CloseNote = null,
            RowVersion = Convert.ToBase64String(new byte[] { 1 })
        };

        var result = await controller.ChangeStage(1, changeModel);

        var viewResult = result.ShouldBeOfType<ViewResult>();
        controller.ModelState["CloseNote"]!.Errors[0].ErrorMessage.ShouldBe("Enter a note about why this opportunity was won.");
    }

    [Fact]
    public async Task Reopen_post_by_manager_succeeds_and_redirects()
    {
        var controller = CreateController(Roles.Manager);
        var reopened = new OpportunityDetail(
            1, "Reopened Deal", 10, "Acme", "CUST-0001", null,
            50000m, 75, 375000m, OpportunityStage.Negotiation, OpportunityStatus.Open,
            Today.AddDays(10), null, null, null, "user-1", "John", null, FixedDate, null, [1]);

        _opportunityService.ReopenAsync(1, Arg.Any<byte[]>())
            .Returns(Result<OpportunityDetail>.Ok(reopened));

        var result = await controller.Reopen(1, Convert.ToBase64String(new byte[] { 1 }));

        var redirect = result.ShouldBeOfType<RedirectToActionResult>();
        redirect.ActionName.ShouldBe(nameof(OpportunitiesController.Details));
        _flash.Received().Success(Arg.Is<string>(s => s.Contains("reopened")));
    }

    [Fact]
    public async Task Amount_binder_accepts_indian_grouping_and_rejects_three_decimals()
    {
        var binder = new IndianDecimalModelBinder();

        // 1. Accepts 12,50,000
        var ctx1 = CreateBindingContext("12,50,000");
        await binder.BindModelAsync(ctx1);
        ctx1.Result.IsModelSet.ShouldBeTrue();
        ctx1.Result.Model.ShouldBe(1250000m);

        // 2. Accepts 1250000.50
        var ctx2 = CreateBindingContext("1250000.50");
        await binder.BindModelAsync(ctx2);
        ctx2.Result.IsModelSet.ShouldBeTrue();
        ctx2.Result.Model.ShouldBe(1250000.50m);

        // 3. Accepts symbol and spaces: ₹ 1,25,000.5
        var ctx3 = CreateBindingContext(" ₹ 1,25,000.5 ");
        await binder.BindModelAsync(ctx3);
        ctx3.Result.IsModelSet.ShouldBeTrue();
        ctx3.Result.Model.ShouldBe(125000.5m);

        // 4. Rejects more than 2 decimals: 1000.123
        var ctx4 = CreateBindingContext("1000.123");
        await binder.BindModelAsync(ctx4);
        ctx4.Result.IsModelSet.ShouldBeFalse();
        ctx4.ModelState["Amount"]!.Errors[0].ErrorMessage.ShouldBe("Enter an amount with at most 2 decimal places.");
    }

    private static DefaultModelBindingContext CreateBindingContext(string value)
    {
        var vp = new SimpleValueProvider(new Dictionary<string, string> { ["Amount"] = value });
        var modelState = new ModelStateDictionary();
        return new DefaultModelBindingContext
        {
            ModelName = "Amount",
            ValueProvider = vp,
            ModelState = modelState,
            ModelMetadata = new EmptyModelMetadataProvider().GetMetadataForType(typeof(decimal))
        };
    }

    private sealed class SimpleValueProvider : IValueProvider
    {
        private readonly Dictionary<string, string> _values;
        public SimpleValueProvider(Dictionary<string, string> values) => _values = values;
        public bool ContainsPrefix(string prefix) => _values.ContainsKey(prefix);
        public ValueProviderResult GetValue(string key) =>
            _values.TryGetValue(key, out var val) ? new ValueProviderResult(val) : ValueProviderResult.None;
    }
}

using AcxiomCrm.Application.Audit;
using AcxiomCrm.Application.Common;
using AcxiomCrm.Domain.Enums;
using AcxiomCrm.Web.Controllers;
using AcxiomCrm.Web.ViewModels.Audit;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using Shouldly;

namespace AcxiomCrm.Web.Tests;

public class AuditControllerTests
{
    private readonly IAuditQueryService _auditQueryService = Substitute.For<IAuditQueryService>();

    private AuditController CreateController() => new(_auditQueryService);

    [Fact]
    public async Task Index_renders_rows_and_filters()
    {
        var controller = CreateController();
        var items = new List<AuditListItem>
        {
            new(1, new DateTime(2026, 10, 16, 10, 0, 0, DateTimeKind.Utc), "admin", "Customers", "Create", "Customer", "101", AuditResult.Success, "127.0.0.1", null),
            new(2, new DateTime(2026, 10, 16, 11, 0, 0, DateTimeKind.Utc), "john", "Authentication", "Login", "User", "user-1", AuditResult.Success, "127.0.0.1", null)
        };
        _auditQueryService.ListModulesAsync().Returns(["Authentication", "Customers"]);
        _auditQueryService.SearchAsync(Arg.Any<AuditFilter>(), Arg.Any<PageQuery>())
            .Returns(new PagedResult<AuditListItem>(items, 1, 20, 2));

        var result = await controller.Index(module: "Customers");

        var viewResult = result.ShouldBeOfType<ViewResult>();
        var model = viewResult.Model.ShouldBeOfType<AuditIndexViewModel>();

        model.Items.Total.ShouldBe(2);
        model.Items.Items.Count.ShouldBe(2);
        model.Modules.ShouldContain("Customers");
        model.Filter.Module.ShouldBe("Customers");
    }

    [Fact]
    public async Task Invalid_date_range_shows_validation_message()
    {
        var controller = CreateController();
        _auditQueryService.ListModulesAsync().Returns(["Authentication"]);

        var from = new DateOnly(2026, 10, 20);
        var to = new DateOnly(2026, 10, 10); // From is after To!

        var result = await controller.Index(from: from, to: to);

        var viewResult = result.ShouldBeOfType<ViewResult>();
        controller.ModelState.ContainsKey("From").ShouldBeTrue();
        controller.ModelState["From"]!.Errors.ShouldContain(e => e.ErrorMessage == "Start date must not be after end date.");
    }

    [Fact]
    public async Task Details_renders_detail_view_model()
    {
        var controller = CreateController();
        var header = new AuditListItem(5, new DateTime(2026, 10, 16, 12, 0, 0, DateTimeKind.Utc), "admin", "Customers", "Update", "Customer", "101", AuditResult.Success, "127.0.0.1", null);
        var changes = new List<AuditChange>
        {
            new("Email", "old@example.com", "new@example.com")
        };
        var detail = new AuditDetail(header, changes, "corr-123");

        _auditQueryService.GetAsync(5).Returns(Result<AuditDetail>.Ok(detail));

        var result = await controller.Details(5);

        var viewResult = result.ShouldBeOfType<ViewResult>();
        var model = viewResult.Model.ShouldBeOfType<AuditDetailViewModel>();
        model.Detail.Header.Id.ShouldBe(5);
        model.Detail.Changes.Count.ShouldBe(1);
        model.Detail.Changes[0].Property.ShouldBe("Email");
    }

    [Fact]
    public async Task Details_returns_NotFound_when_record_missing()
    {
        var controller = CreateController();
        _auditQueryService.GetAsync(999).Returns(Result<AuditDetail>.Fail(ErrorKind.NotFound));

        var result = await controller.Details(999);

        result.ShouldBeOfType<NotFoundResult>();
    }
}

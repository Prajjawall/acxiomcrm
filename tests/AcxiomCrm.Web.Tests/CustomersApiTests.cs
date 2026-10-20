using AcxiomCrm.Application.Abstractions;
using AcxiomCrm.Application.Common;
using AcxiomCrm.Application.Customers;
using AcxiomCrm.Domain.Enums;
using AcxiomCrm.Domain.Security;
using AcxiomCrm.Domain.Validation;
using AcxiomCrm.Web.Controllers.Api;
using AcxiomCrm.Web.Controllers.Api.Dtos;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using Shouldly;

namespace AcxiomCrm.Web.Tests;

public class CustomersApiTests
{
    private static readonly DateTime FixedNowUtc = new(2026, 10, 20, 12, 0, 0, DateTimeKind.Utc);
    private readonly ICustomerService _customerService = Substitute.For<ICustomerService>();
    private readonly ICurrentUser _currentUser = Substitute.For<ICurrentUser>();

    private CustomersApiController CreateController(string role = Roles.Admin, string userId = "user-1")
    {
        _currentUser.Role.Returns(role);
        _currentUser.UserId.Returns(userId);

        var httpContext = new DefaultHttpContext();
        httpContext.TraceIdentifier = "trace-test-123";

        return new CustomersApiController(_customerService, _currentUser)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = httpContext
            }
        };
    }

    [Fact]
    public async Task List_returns_paged_customers()
    {
        // Arrange
        var controller = CreateController();
        var items = new List<CustomerListItem>
        {
            new(1, "CUST-0001", "Acme Corp", "Acme", "acme@example.com", "9876543210", CustomerStatus.Active, "user-1", "Alice", FixedNowUtc)
        };
        _customerService.SearchAsync(Arg.Any<CustomerFilter>(), Arg.Any<PageQuery>(), Arg.Any<CancellationToken>())
            .Returns(new PagedResult<CustomerListItem>(items, 1, 25, 1));

        // Act
        var result = await controller.List(null, null, null, 1, 25, null, false, CancellationToken.None);

        // Assert
        var okResult = result.ShouldBeOfType<OkObjectResult>();
        var paged = okResult.Value.ShouldBeOfType<PagedDto<CustomerDto>>();
        paged.Items.Count.ShouldBe(1);
        paged.Items[0].Name.ShouldBe("Acme Corp");
        paged.Items[0].Code.ShouldBe("CUST-0001");
        paged.Total.ShouldBe(1);
    }

    [Fact]
    public async Task Get_by_id_returns_customer_when_found()
    {
        // Arrange
        var controller = CreateController();
        var detail = new CustomerDetail(
            1, "CUST-0001", "Acme Corp", "Acme", "acme@example.com", "9876543210",
            "123 Street", "City", "State", CustomerStatus.Active, "user-1", "Alice",
            "Notes", FixedNowUtc, null, [1, 2, 3]);

        _customerService.GetAsync(1, Arg.Any<CancellationToken>())
            .Returns(Result<CustomerDetail>.Ok(detail));

        // Act
        var result = await controller.Get(1, CancellationToken.None);

        // Assert
        var okResult = result.ShouldBeOfType<OkObjectResult>();
        var dto = okResult.Value.ShouldBeOfType<CustomerDto>();
        dto.Id.ShouldBe(1);
        dto.Name.ShouldBe("Acme Corp");
        dto.Owner?.Id.ShouldBe("user-1");
    }

    [Fact]
    public async Task Get_by_id_returns_404_when_not_found_or_out_of_scope()
    {
        // Arrange
        var controller = CreateController();
        _customerService.GetAsync(999, Arg.Any<CancellationToken>())
            .Returns(Result<CustomerDetail>.Fail(ErrorKind.NotFound));

        // Act
        var result = await controller.Get(999, CancellationToken.None);

        // Assert
        var notFound = result.ShouldBeOfType<NotFoundObjectResult>();
        var problem = notFound.Value.ShouldBeOfType<ProblemDetails>();
        problem.Status.ShouldBe(StatusCodes.Status404NotFound);
    }

    [Fact]
    public async Task Create_returns_201_with_location_and_body()
    {
        // Arrange
        var controller = CreateController();
        var detail = new CustomerDetail(
            10, "CUST-0010", "Beta Corp", null, "beta@example.com", "9876543211",
            null, null, null, CustomerStatus.Active, "user-1", "Alice",
            null, FixedNowUtc, null, [1]);

        _customerService.CreateAsync(Arg.Any<CreateCustomerCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result<CustomerDetail>.Ok(detail));

        var request = new CreateCustomerRequest("Beta Corp", null, "beta@example.com", "9876543211", null, null, null, CustomerStatus.Active, null, null);

        // Act
        var result = await controller.Create(request, CancellationToken.None);

        // Assert
        var createdResult = result.ShouldBeOfType<CreatedResult>();
        createdResult.Location.ShouldBe("/api/customers/10");
        var dto = createdResult.Value.ShouldBeOfType<CustomerDto>();
        dto.Id.ShouldBe(10);
        dto.Name.ShouldBe("Beta Corp");
    }

    [Fact]
    public async Task Create_returns_400_with_camel_case_keys_when_validation_fails()
    {
        // Arrange
        var controller = CreateController();
        _customerService.CreateAsync(Arg.Any<CreateCustomerCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result<CustomerDetail>.Fail(ErrorKind.Validation, new FieldError("Email", "Invalid email format.")));

        var request = new CreateCustomerRequest("", null, "invalid", "123", null, null, null, CustomerStatus.Active, null, null);

        // Act
        var result = await controller.Create(request, CancellationToken.None);

        // Assert
        var badResult = result.ShouldBeOfType<BadRequestObjectResult>();
        var problem = badResult.Value.ShouldBeOfType<ValidationProblemDetails>();
        problem.Errors.ShouldContainKey("email");
        problem.Errors["email"].ShouldContain("Invalid email format.");
    }

    [Fact]
    public async Task Delete_returns_204_when_successful()
    {
        // Arrange
        var controller = CreateController();
        _customerService.DeleteAsync(5, Arg.Any<CancellationToken>())
            .Returns(Result.Ok());

        // Act
        var result = await controller.Delete(5, CancellationToken.None);

        // Assert
        result.ShouldBeOfType<NoContentResult>();
    }

    [Fact]
    public async Task Delete_returns_409_when_open_opportunities_exist()
    {
        // Arrange
        var controller = CreateController();
        _customerService.DeleteAsync(5, Arg.Any<CancellationToken>())
            .Returns(Result.Fail(ErrorKind.Conflict, new FieldError(string.Empty, "Cannot delete customer with open opportunities.")));

        // Act
        var result = await controller.Delete(5, CancellationToken.None);

        // Assert
        var conflictResult = result.ShouldBeOfType<ConflictObjectResult>();
        var problem = conflictResult.Value.ShouldBeOfType<ProblemDetails>();
        problem.Status.ShouldBe(StatusCodes.Status409Conflict);
        problem.Detail.ShouldBe("Cannot delete customer with open opportunities.");
    }
}

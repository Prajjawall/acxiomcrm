using AcxiomCrm.Application.Common;
using AcxiomCrm.Application.Customers;
using AcxiomCrm.Domain.Enums;
using AcxiomCrm.Domain.Security;
using AcxiomCrm.Domain.Validation;
using AcxiomCrm.TestSupport;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace AcxiomCrm.Application.Tests;

public class CustomerValidationTests : DbTestBase
{
    public CustomerValidationTests(SqlServerContainerFixture fixture) : base(fixture)
    {
    }

    [Fact]
    public async Task Blank_name_is_rejected_with_canonical_message()
    {
        await using var ctx = Db.CreateContext();
        var users = await TestUsers.CreateStandardAsync(ctx);

        using var harness = ServiceHarness.Create(Db, users.AdminId, Roles.Admin);
        var service = harness.Services.GetRequiredService<ICustomerService>();

        var res = await service.CreateAsync(new CreateCustomerCommand(
            "   ", null, "test@example.com", "9876543210", null, null, null, CustomerStatus.Active, null, null));

        res.Succeeded.ShouldBeFalse();
        res.Kind.ShouldBe(ErrorKind.Validation);
        res.Errors.ShouldContain(e => e.Field == "Name" && e.Message == "Customer name is required.");
    }

    [Fact]
    public async Task Name_overflow_is_rejected_with_canonical_message()
    {
        await using var ctx = Db.CreateContext();
        var users = await TestUsers.CreateStandardAsync(ctx);

        using var harness = ServiceHarness.Create(Db, users.AdminId, Roles.Admin);
        var service = harness.Services.GetRequiredService<ICustomerService>();

        var res = await service.CreateAsync(new CreateCustomerCommand(
            new string('a', Limits.Name + 1), null, "test@example.com", "9876543210", null, null, null, CustomerStatus.Active, null, null));

        res.Succeeded.ShouldBeFalse();
        res.Kind.ShouldBe(ErrorKind.Validation);
        res.Errors.ShouldContain(e => e.Field == "Name" && e.Message == "Customer name must not exceed 150 characters.");
    }

    [Fact]
    public async Task Company_overflow_is_rejected_with_canonical_message()
    {
        await using var ctx = Db.CreateContext();
        var users = await TestUsers.CreateStandardAsync(ctx);

        using var harness = ServiceHarness.Create(Db, users.AdminId, Roles.Admin);
        var service = harness.Services.GetRequiredService<ICustomerService>();

        var res = await service.CreateAsync(new CreateCustomerCommand(
            "Acme", new string('c', Limits.Name + 1), "test@example.com", "9876543210", null, null, null, CustomerStatus.Active, null, null));

        res.Succeeded.ShouldBeFalse();
        res.Kind.ShouldBe(ErrorKind.Validation);
        res.Errors.ShouldContain(e => e.Field == "CompanyName" && e.Message == "Company name must not exceed 150 characters.");
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-an-email")]
    public async Task Invalid_email_is_rejected_with_canonical_message(string email)
    {
        await using var ctx = Db.CreateContext();
        var users = await TestUsers.CreateStandardAsync(ctx);

        using var harness = ServiceHarness.Create(Db, users.AdminId, Roles.Admin);
        var service = harness.Services.GetRequiredService<ICustomerService>();

        var res = await service.CreateAsync(new CreateCustomerCommand(
            "Acme", null, email, "9876543210", null, null, null, CustomerStatus.Active, null, null));

        res.Succeeded.ShouldBeFalse();
        res.Kind.ShouldBe(ErrorKind.Validation);
        res.Errors.ShouldContain(e => e.Field == "Email" && e.Message == "Enter a valid email address.");
    }

    [Fact]
    public async Task Email_overflow_is_rejected_with_canonical_message()
    {
        await using var ctx = Db.CreateContext();
        var users = await TestUsers.CreateStandardAsync(ctx);

        using var harness = ServiceHarness.Create(Db, users.AdminId, Roles.Admin);
        var service = harness.Services.GetRequiredService<ICustomerService>();

        var longEmail = $"{new string('a', Limits.Email - 11)}@example.com";
        var res = await service.CreateAsync(new CreateCustomerCommand(
            "Acme", null, longEmail, "9876543210", null, null, null, CustomerStatus.Active, null, null));

        res.Succeeded.ShouldBeFalse();
        res.Kind.ShouldBe(ErrorKind.Validation);
        res.Errors.ShouldContain(e => e.Field == "Email" && e.Message == "Email must not exceed 254 characters.");
    }

    [Theory]
    [InlineData("")]
    [InlineData("12345")]
    [InlineData("5876543210")]
    public async Task Invalid_phone_is_rejected_with_canonical_message(string phone)
    {
        await using var ctx = Db.CreateContext();
        var users = await TestUsers.CreateStandardAsync(ctx);

        using var harness = ServiceHarness.Create(Db, users.AdminId, Roles.Admin);
        var service = harness.Services.GetRequiredService<ICustomerService>();

        var res = await service.CreateAsync(new CreateCustomerCommand(
            "Acme", null, "test@example.com", phone, null, null, null, CustomerStatus.Active, null, null));

        res.Succeeded.ShouldBeFalse();
        res.Kind.ShouldBe(ErrorKind.Validation);
        res.Errors.ShouldContain(e => e.Field == "Phone" && e.Message == "Enter a valid phone number.");
    }

    [Fact]
    public async Task Address_overflow_is_rejected_with_canonical_message()
    {
        await using var ctx = Db.CreateContext();
        var users = await TestUsers.CreateStandardAsync(ctx);

        using var harness = ServiceHarness.Create(Db, users.AdminId, Roles.Admin);
        var service = harness.Services.GetRequiredService<ICustomerService>();

        var res = await service.CreateAsync(new CreateCustomerCommand(
            "Acme", null, "test@example.com", "9876543210", new string('a', Limits.Address + 1), null, null, CustomerStatus.Active, null, null));

        res.Succeeded.ShouldBeFalse();
        res.Kind.ShouldBe(ErrorKind.Validation);
        res.Errors.ShouldContain(e => e.Field == "Address" && e.Message == "Address must not exceed 300 characters.");
    }

    [Fact]
    public async Task City_and_State_overflow_are_rejected()
    {
        await using var ctx = Db.CreateContext();
        var users = await TestUsers.CreateStandardAsync(ctx);

        using var harness = ServiceHarness.Create(Db, users.AdminId, Roles.Admin);
        var service = harness.Services.GetRequiredService<ICustomerService>();

        var res = await service.CreateAsync(new CreateCustomerCommand(
            "Acme", null, "test@example.com", "9876543210", null, new string('c', Limits.CityState + 1), new string('s', Limits.CityState + 1), CustomerStatus.Active, null, null));

        res.Succeeded.ShouldBeFalse();
        res.Kind.ShouldBe(ErrorKind.Validation);
        res.Errors.ShouldContain(e => e.Field == "City" && e.Message == "City must not exceed 100 characters.");
        res.Errors.ShouldContain(e => e.Field == "State" && e.Message == "State must not exceed 100 characters.");
    }

    [Fact]
    public async Task Notes_overflow_is_rejected_with_canonical_message()
    {
        await using var ctx = Db.CreateContext();
        var users = await TestUsers.CreateStandardAsync(ctx);

        using var harness = ServiceHarness.Create(Db, users.AdminId, Roles.Admin);
        var service = harness.Services.GetRequiredService<ICustomerService>();

        var res = await service.CreateAsync(new CreateCustomerCommand(
            "Acme", null, "test@example.com", "9876543210", null, null, null, CustomerStatus.Active, null, new string('n', Limits.Notes + 1)));

        res.Succeeded.ShouldBeFalse();
        res.Kind.ShouldBe(ErrorKind.Validation);
        res.Errors.ShouldContain(e => e.Field == "Notes" && e.Message == "Notes must not exceed 2000 characters.");
    }

    [Fact]
    public async Task Status_out_of_range_rejected()
    {
        await using var ctx = Db.CreateContext();
        var users = await TestUsers.CreateStandardAsync(ctx);

        using var harness = ServiceHarness.Create(Db, users.AdminId, Roles.Admin);
        var service = harness.Services.GetRequiredService<ICustomerService>();

        var res = await service.CreateAsync(new CreateCustomerCommand(
            "Acme", null, "test@example.com", "9876543210", null, null, null, (CustomerStatus)99, null, null));

        res.Succeeded.ShouldBeFalse();
        res.Kind.ShouldBe(ErrorKind.Validation);
        res.Errors.ShouldContain(e => e.Field == "Status");
    }

    [Fact]
    public async Task Deactivated_owner_rejected()
    {
        await using var ctx = Db.CreateContext();
        var users = await TestUsers.CreateStandardAsync(ctx);

        using var harness = ServiceHarness.Create(Db, users.AdminId, Roles.Admin);
        var service = harness.Services.GetRequiredService<ICustomerService>();

        // users.ExecN2Id is inactive
        var res = await service.CreateAsync(new CreateCustomerCommand(
            "Acme", null, "test@example.com", "9876543210", null, null, null, CustomerStatus.Active, users.ExecN2Id, null));

        res.Succeeded.ShouldBeFalse();
        res.Kind.ShouldBe(ErrorKind.Validation);
        res.Errors.ShouldContain(e => e.Field == "OwnerId");
    }
}

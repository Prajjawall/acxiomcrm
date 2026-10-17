using AcxiomCrm.Application.Common;
using AcxiomCrm.Application.Leads;
using AcxiomCrm.Domain.Enums;
using AcxiomCrm.Domain.Security;
using AcxiomCrm.Domain.Validation;
using AcxiomCrm.TestSupport;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace AcxiomCrm.Application.Tests;

public class LeadValidationTests : DbTestBase
{
    public LeadValidationTests(SqlServerContainerFixture fixture) : base(fixture)
    {
    }

    [Fact]
    public async Task Blank_name_is_rejected_with_canonical_message()
    {
        await using var ctx = Db.CreateContext();
        var users = await TestUsers.CreateStandardAsync(ctx);

        using var harness = ServiceHarness.Create(Db, users.AdminId, Roles.Admin);
        var service = harness.Services.GetRequiredService<ILeadService>();

        var res = await service.CreateAsync(new CreateLeadCommand(
            "   ", null, "test@example.com", null, LeadSource.Website, LeadPriority.Medium, 100m, null, null));

        res.Succeeded.ShouldBeFalse();
        res.Kind.ShouldBe(ErrorKind.Validation);
        res.Errors.ShouldContain(e => e.Field == "Name" && e.Message == "Lead name is required.");
    }

    [Fact]
    public async Task Name_overflow_is_rejected_with_canonical_message()
    {
        await using var ctx = Db.CreateContext();
        var users = await TestUsers.CreateStandardAsync(ctx);

        using var harness = ServiceHarness.Create(Db, users.AdminId, Roles.Admin);
        var service = harness.Services.GetRequiredService<ILeadService>();

        var res = await service.CreateAsync(new CreateLeadCommand(
            new string('a', Limits.Name + 1), null, "test@example.com", null, LeadSource.Website, LeadPriority.Medium, 100m, null, null));

        res.Succeeded.ShouldBeFalse();
        res.Kind.ShouldBe(ErrorKind.Validation);
        res.Errors.ShouldContain(e => e.Field == "Name" && e.Message == "Lead name must not exceed 150 characters.");
    }

    [Fact]
    public async Task Company_overflow_is_rejected_with_canonical_message()
    {
        await using var ctx = Db.CreateContext();
        var users = await TestUsers.CreateStandardAsync(ctx);

        using var harness = ServiceHarness.Create(Db, users.AdminId, Roles.Admin);
        var service = harness.Services.GetRequiredService<ILeadService>();

        var res = await service.CreateAsync(new CreateLeadCommand(
            "Acme", new string('c', Limits.Name + 1), "test@example.com", null, LeadSource.Website, LeadPriority.Medium, 100m, null, null));

        res.Succeeded.ShouldBeFalse();
        res.Kind.ShouldBe(ErrorKind.Validation);
        res.Errors.ShouldContain(e => e.Field == "CompanyName" && e.Message == "Company name must not exceed 150 characters.");
    }

    [Fact]
    public async Task Neither_email_nor_phone_is_rejected_on_both_fields()
    {
        await using var ctx = Db.CreateContext();
        var users = await TestUsers.CreateStandardAsync(ctx);

        using var harness = ServiceHarness.Create(Db, users.AdminId, Roles.Admin);
        var service = harness.Services.GetRequiredService<ILeadService>();

        var res = await service.CreateAsync(new CreateLeadCommand(
            "Acme Lead", null, null, null, LeadSource.Website, LeadPriority.Medium, 100m, null, null));

        res.Succeeded.ShouldBeFalse();
        res.Kind.ShouldBe(ErrorKind.Validation);
        res.Errors.ShouldContain(e => e.Field == "Email" && e.Message == "Enter an email address or a phone number.");
        res.Errors.ShouldContain(e => e.Field == "Phone" && e.Message == "Enter an email address or a phone number.");
    }

    [Fact]
    public async Task Single_contact_method_is_allowed()
    {
        await using var ctx = Db.CreateContext();
        var users = await TestUsers.CreateStandardAsync(ctx);

        using var harness = ServiceHarness.Create(Db, users.AdminId, Roles.Admin);
        var service = harness.Services.GetRequiredService<ILeadService>();

        // Only email
        var r1 = await service.CreateAsync(new CreateLeadCommand(
            "Email Only Lead", null, "only@example.com", null, LeadSource.Website, LeadPriority.Medium, 100m, null, null));
        r1.Succeeded.ShouldBeTrue();

        // Only phone
        var r2 = await service.CreateAsync(new CreateLeadCommand(
            "Phone Only Lead", null, null, "9876543210", LeadSource.Website, LeadPriority.Medium, 100m, null, null));
        r2.Succeeded.ShouldBeTrue();
    }

    [Fact]
    public async Task Invalid_email_is_rejected_with_canonical_message()
    {
        await using var ctx = Db.CreateContext();
        var users = await TestUsers.CreateStandardAsync(ctx);

        using var harness = ServiceHarness.Create(Db, users.AdminId, Roles.Admin);
        var service = harness.Services.GetRequiredService<ILeadService>();

        var res = await service.CreateAsync(new CreateLeadCommand(
            "Acme", null, "not-an-email", null, LeadSource.Website, LeadPriority.Medium, 100m, null, null));

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
        var service = harness.Services.GetRequiredService<ILeadService>();

        var longEmail = $"{new string('a', Limits.Email - 11)}@example.com";
        var res = await service.CreateAsync(new CreateLeadCommand(
            "Acme", null, longEmail, null, LeadSource.Website, LeadPriority.Medium, 100m, null, null));

        res.Succeeded.ShouldBeFalse();
        res.Kind.ShouldBe(ErrorKind.Validation);
        res.Errors.ShouldContain(e => e.Field == "Email" && e.Message == "Email must not exceed 254 characters.");
    }

    [Theory]
    [InlineData("12345")]
    [InlineData("5876543210")]
    public async Task Invalid_phone_is_rejected_with_canonical_message(string phone)
    {
        await using var ctx = Db.CreateContext();
        var users = await TestUsers.CreateStandardAsync(ctx);

        using var harness = ServiceHarness.Create(Db, users.AdminId, Roles.Admin);
        var service = harness.Services.GetRequiredService<ILeadService>();

        var res = await service.CreateAsync(new CreateLeadCommand(
            "Acme", null, null, phone, LeadSource.Website, LeadPriority.Medium, 100m, null, null));

        res.Succeeded.ShouldBeFalse();
        res.Kind.ShouldBe(ErrorKind.Validation);
        res.Errors.ShouldContain(e => e.Field == "Phone" && e.Message == "Enter a valid phone number.");
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(100000000.01)]
    public async Task Expected_value_out_of_bounds_is_rejected(decimal val)
    {
        await using var ctx = Db.CreateContext();
        var users = await TestUsers.CreateStandardAsync(ctx);

        using var harness = ServiceHarness.Create(Db, users.AdminId, Roles.Admin);
        var service = harness.Services.GetRequiredService<ILeadService>();

        var res = await service.CreateAsync(new CreateLeadCommand(
            "Acme", null, "test@example.com", null, LeadSource.Website, LeadPriority.Medium, val, null, null));

        res.Succeeded.ShouldBeFalse();
        res.Kind.ShouldBe(ErrorKind.Validation);
        res.Errors.ShouldContain(e => e.Field == "ExpectedValue" && e.Message == "Expected value must be between 0 and 10,00,00,000.");
    }

    [Fact]
    public async Task Expected_value_too_many_decimals_is_rejected()
    {
        await using var ctx = Db.CreateContext();
        var users = await TestUsers.CreateStandardAsync(ctx);

        using var harness = ServiceHarness.Create(Db, users.AdminId, Roles.Admin);
        var service = harness.Services.GetRequiredService<ILeadService>();

        var res = await service.CreateAsync(new CreateLeadCommand(
            "Acme", null, "test@example.com", null, LeadSource.Website, LeadPriority.Medium, 10.005m, null, null));

        res.Succeeded.ShouldBeFalse();
        res.Kind.ShouldBe(ErrorKind.Validation);
        res.Errors.ShouldContain(e => e.Field == "ExpectedValue" && e.Message == "Expected value cannot have more than 2 decimal places.");
    }

    [Fact]
    public async Task Notes_overflow_is_rejected_with_canonical_message()
    {
        await using var ctx = Db.CreateContext();
        var users = await TestUsers.CreateStandardAsync(ctx);

        using var harness = ServiceHarness.Create(Db, users.AdminId, Roles.Admin);
        var service = harness.Services.GetRequiredService<ILeadService>();

        var res = await service.CreateAsync(new CreateLeadCommand(
            "Acme", null, "test@example.com", null, LeadSource.Website, LeadPriority.Medium, 100m, null, new string('n', Limits.Notes + 1)));

        res.Succeeded.ShouldBeFalse();
        res.Kind.ShouldBe(ErrorKind.Validation);
        res.Errors.ShouldContain(e => e.Field == "Notes" && e.Message == "Notes must not exceed 2000 characters.");
    }
}

using AcxiomCrm.Application.Common;
using AcxiomCrm.Application.Customers;
using AcxiomCrm.Domain.Enums;
using AcxiomCrm.Domain.Security;
using AcxiomCrm.Domain.Validation;
using AcxiomCrm.TestSupport;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace AcxiomCrm.Application.Tests;

public class CustomerDuplicateTests : DbTestBase
{
    public CustomerDuplicateTests(SqlServerContainerFixture fixture) : base(fixture)
    {
    }

    [Fact]
    public async Task Email_case_insensitive_duplicate_is_rejected()
    {
        await using var ctx = Db.CreateContext();
        var users = await TestUsers.CreateStandardAsync(ctx);

        using var harness = ServiceHarness.Create(Db, users.AdminId, Roles.Admin);
        var service = harness.Services.GetRequiredService<ICustomerService>();

        var first = await service.CreateAsync(new CreateCustomerCommand(
            "Customer 1", null, "asha@acme.in", "9876543210", null, null, null, CustomerStatus.Active, null, null));
        first.Succeeded.ShouldBeTrue();

        var dup = await service.CreateAsync(new CreateCustomerCommand(
            "Customer 2", null, "Asha@Acme.IN", "9876543211", null, null, null, CustomerStatus.Active, null, null));

        dup.Succeeded.ShouldBeFalse();
        dup.Kind.ShouldBe(ErrorKind.Conflict);
        dup.Errors.ShouldContain(e => e.Field == "Email" && e.Message == Messages.DuplicateEmail);
    }

    [Fact]
    public async Task Phone_normalised_duplicate_is_rejected()
    {
        await using var ctx = Db.CreateContext();
        var users = await TestUsers.CreateStandardAsync(ctx);

        using var harness = ServiceHarness.Create(Db, users.AdminId, Roles.Admin);
        var service = harness.Services.GetRequiredService<ICustomerService>();

        var first = await service.CreateAsync(new CreateCustomerCommand(
            "Customer 1", null, "c1@example.com", "9876543210", null, null, null, CustomerStatus.Active, null, null));
        first.Succeeded.ShouldBeTrue();

        var dup = await service.CreateAsync(new CreateCustomerCommand(
            "Customer 2", null, "c2@example.com", "+91 98765 43210", null, null, null, CustomerStatus.Active, null, null));

        dup.Succeeded.ShouldBeFalse();
        dup.Kind.ShouldBe(ErrorKind.Conflict);
        dup.Errors.ShouldContain(e => e.Field == "Phone" && e.Message == Messages.DuplicatePhone);
    }

    [Fact]
    public async Task Soft_deleted_customer_email_and_phone_can_be_reused()
    {
        await using var ctx = Db.CreateContext();
        var users = await TestUsers.CreateStandardAsync(ctx);

        using var harness = ServiceHarness.Create(Db, users.AdminId, Roles.Admin);
        var service = harness.Services.GetRequiredService<ICustomerService>();

        var c1 = await service.CreateAsync(new CreateCustomerCommand(
            "Customer 1", null, "reuse@example.com", "9876543210", null, null, null, CustomerStatus.Active, null, null));
        c1.Succeeded.ShouldBeTrue();

        var del = await service.DeleteAsync(c1.Value!.Id);
        del.Succeeded.ShouldBeTrue();

        var c2 = await service.CreateAsync(new CreateCustomerCommand(
            "Customer 2", null, "reuse@example.com", "9876543210", null, null, null, CustomerStatus.Active, null, null));

        c2.Succeeded.ShouldBeTrue();
    }

    [Fact]
    public async Task Duplicate_message_does_not_leak_other_owners_record()
    {
        await using var ctx = Db.CreateContext();
        var users = await TestUsers.CreateStandardAsync(ctx);

        // ExecSouth creates customer
        using (var southHarness = ServiceHarness.Create(Db, users.ExecS1Id, Roles.SalesExecutive))
        {
            var southService = southHarness.Services.GetRequiredService<ICustomerService>();
            var s = await southService.CreateAsync(new CreateCustomerCommand(
                "Secret South Customer", "Top Secret Co", "secret@example.com", "9876543210", null, null, null, CustomerStatus.Active, null, null));
            s.Succeeded.ShouldBeTrue();
        }

        // ExecNorth tries to create customer with same email & phone
        using (var northHarness = ServiceHarness.Create(Db, users.ExecN1Id, Roles.SalesExecutive))
        {
            var northService = northHarness.Services.GetRequiredService<ICustomerService>();
            var n = await northService.CreateAsync(new CreateCustomerCommand(
                "North Customer", null, "secret@example.com", "9876543210", null, null, null, CustomerStatus.Active, null, null));

            n.Succeeded.ShouldBeFalse();
            n.Kind.ShouldBe(ErrorKind.Conflict);
            n.Errors.Count.ShouldBe(2);
            n.Errors.ShouldContain(e => e.Field == "Email" && e.Message == Messages.DuplicateEmail);
            n.Errors.ShouldContain(e => e.Field == "Phone" && e.Message == Messages.DuplicatePhone);

            // Verify no leakage of South details in errors
            foreach (var err in n.Errors)
            {
                err.Message.ShouldNotContain("Secret South");
                err.Message.ShouldNotContain("Top Secret");
                err.Message.ShouldNotContain(users.ExecS1Id);
            }
        }
    }

    [Fact]
    public async Task Update_without_changing_email_or_phone_does_not_trip_duplicate()
    {
        await using var ctx = Db.CreateContext();
        var users = await TestUsers.CreateStandardAsync(ctx);

        using var harness = ServiceHarness.Create(Db, users.AdminId, Roles.Admin);
        var service = harness.Services.GetRequiredService<ICustomerService>();

        var c = await service.CreateAsync(new CreateCustomerCommand(
            "Customer 1", null, "self@example.com", "9876543210", null, null, null, CustomerStatus.Active, null, null));
        c.Succeeded.ShouldBeTrue();

        var update = await service.UpdateAsync(c.Value!.Id, new UpdateCustomerCommand(
            "Customer 1 Renamed", null, "self@example.com", "9876543210", null, null, null, CustomerStatus.Active, null, null, c.Value.RowVersion));

        update.Succeeded.ShouldBeTrue();
    }

    [Fact]
    public async Task Concurrent_creates_with_same_email_yield_one_success_one_conflict()
    {
        await using var ctx = Db.CreateContext();
        var users = await TestUsers.CreateStandardAsync(ctx);

        var task1 = Task.Run(async () =>
        {
            using var harness = ServiceHarness.Create(Db, users.ExecN1Id, Roles.SalesExecutive);
            var service = harness.Services.GetRequiredService<ICustomerService>();
            return await service.CreateAsync(new CreateCustomerCommand(
                "User 1", null, "concurrent@example.com", "9876543211", null, null, null, CustomerStatus.Active, null, null));
        });

        var task2 = Task.Run(async () =>
        {
            using var harness = ServiceHarness.Create(Db, users.ExecS1Id, Roles.SalesExecutive);
            var service = harness.Services.GetRequiredService<ICustomerService>();
            return await service.CreateAsync(new CreateCustomerCommand(
                "User 2", null, "concurrent@example.com", "9876543212", null, null, null, CustomerStatus.Active, null, null));
        });

        var results = await Task.WhenAll(task1, task2);

        var succeeded = results.Count(r => r.Succeeded);
        var conflicts = results.Count(r => !r.Succeeded && r.Kind == ErrorKind.Conflict);

        succeeded.ShouldBe(1);
        conflicts.ShouldBe(1);
    }
}

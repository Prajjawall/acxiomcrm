using AcxiomCrm.Application.Common;
using AcxiomCrm.Application.Customers;
using AcxiomCrm.Domain.Enums;
using AcxiomCrm.Domain.Security;
using AcxiomCrm.TestSupport;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace AcxiomCrm.Application.Tests;

public class CustomerIdorTests : DbTestBase
{
    public CustomerIdorTests(SqlServerContainerFixture fixture) : base(fixture)
    {
    }

    [Fact]
    public async Task ExecutiveA_and_other_manager_cannot_access_ExecutiveB_customer()
    {
        await using var ctx = Db.CreateContext();
        var users = await TestUsers.CreateStandardAsync(ctx);

        // ExecNorth1 creates a customer
        int northCustomerId;
        byte[] rowVersion;
        using (var n1Harness = ServiceHarness.Create(Db, users.ExecN1Id, Roles.SalesExecutive))
        {
            var service = n1Harness.Services.GetRequiredService<ICustomerService>();
            var res = await service.CreateAsync(new CreateCustomerCommand(
                "North Customer", null, "north.cust@example.com", "9876543210", null, null, null, CustomerStatus.Active, null, null));
            res.Succeeded.ShouldBeTrue();
            northCustomerId = res.Value!.Id;
            rowVersion = res.Value.RowVersion;
        }

        // ExecSouth1 (different executive, different team)
        using (var s1Harness = ServiceHarness.Create(Db, users.ExecS1Id, Roles.SalesExecutive))
        {
            var service = s1Harness.Services.GetRequiredService<ICustomerService>();

            // Get -> NotFound
            var get = await service.GetAsync(northCustomerId);
            get.Succeeded.ShouldBeFalse();
            get.Kind.ShouldBe(ErrorKind.NotFound);

            // Update -> NotFound
            var update = await service.UpdateAsync(northCustomerId, new UpdateCustomerCommand(
                "Hacked", null, "hacked@example.com", "9876543211", null, null, null, CustomerStatus.Active, null, null, rowVersion));
            update.Succeeded.ShouldBeFalse();
            update.Kind.ShouldBe(ErrorKind.NotFound);

            // Delete -> NotFound
            var delete = await service.DeleteAsync(northCustomerId);
            delete.Succeeded.ShouldBeFalse();
            delete.Kind.ShouldBe(ErrorKind.NotFound);

            // Search -> excludes
            var search = await service.SearchAsync(new CustomerFilter(), new PageQuery());
            search.Items.ShouldNotContain(c => c.Id == northCustomerId);
        }

        // SouthManager (different team manager)
        using (var southMgrHarness = ServiceHarness.Create(Db, users.SouthManagerId, Roles.Manager))
        {
            var service = southMgrHarness.Services.GetRequiredService<ICustomerService>();

            var get = await service.GetAsync(northCustomerId);
            get.Succeeded.ShouldBeFalse();
            get.Kind.ShouldBe(ErrorKind.NotFound);

            var update = await service.UpdateAsync(northCustomerId, new UpdateCustomerCommand(
                "Hacked", null, "hacked@example.com", "9876543211", null, null, null, CustomerStatus.Active, null, null, rowVersion));
            update.Succeeded.ShouldBeFalse();
            update.Kind.ShouldBe(ErrorKind.NotFound);

            var delete = await service.DeleteAsync(northCustomerId);
            delete.Succeeded.ShouldBeFalse();
            delete.Kind.ShouldBe(ErrorKind.NotFound);

            var search = await service.SearchAsync(new CustomerFilter(), new PageQuery());
            search.Items.ShouldNotContain(c => c.Id == northCustomerId);
        }

        // NorthManager (same team manager) can read, update, delete
        using (var northMgrHarness = ServiceHarness.Create(Db, users.NorthManagerId, Roles.Manager))
        {
            var service = northMgrHarness.Services.GetRequiredService<ICustomerService>();

            var get = await service.GetAsync(northCustomerId);
            get.Succeeded.ShouldBeTrue();
            get.Value!.Name.ShouldBe("North Customer");

            var search = await service.SearchAsync(new CustomerFilter(), new PageQuery());
            search.Items.ShouldContain(c => c.Id == northCustomerId);

            var update = await service.UpdateAsync(northCustomerId, new UpdateCustomerCommand(
                "North Customer Managed", null, "north.cust@example.com", "9876543210", null, null, null, CustomerStatus.Active, null, null, get.Value.RowVersion));
            update.Succeeded.ShouldBeTrue();

            var delete = await service.DeleteAsync(northCustomerId);
            delete.Succeeded.ShouldBeTrue();
        }

        // Admin can access everything
        using (var adminHarness = ServiceHarness.Create(Db, users.AdminId, Roles.Admin))
        {
            var service = adminHarness.Services.GetRequiredService<ICustomerService>();
            var newCust = await service.CreateAsync(new CreateCustomerCommand(
                "Admin Customer", null, "admin.cust@example.com", "9876543212", null, null, null, CustomerStatus.Active, null, null));
            newCust.Succeeded.ShouldBeTrue();

            var get = await service.GetAsync(newCust.Value!.Id);
            get.Succeeded.ShouldBeTrue();

            var search = await service.SearchAsync(new CustomerFilter(), new PageQuery());
            search.Items.ShouldContain(c => c.Id == newCust.Value.Id);
        }
    }
}

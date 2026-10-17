using AcxiomCrm.Application.Common;
using AcxiomCrm.Application.Customers;
using AcxiomCrm.Domain.Entities;
using AcxiomCrm.Domain.Enums;
using AcxiomCrm.Domain.Security;
using AcxiomCrm.TestSupport;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace AcxiomCrm.Application.Tests;

public class CustomerServiceTests : DbTestBase
{
    public CustomerServiceTests(SqlServerContainerFixture fixture) : base(fixture)
    {
    }

    [Fact]
    public async Task Create_as_executive_forces_self_owner()
    {
        await using var ctx = Db.CreateContext();
        var users = await TestUsers.CreateStandardAsync(ctx);

        using var harness = ServiceHarness.Create(Db, users.ExecN1Id, Roles.SalesExecutive);
        var service = harness.Services.GetRequiredService<ICustomerService>();

        var cmd = new CreateCustomerCommand(
            Name: "Customer One",
            CompanyName: "Acme",
            Email: "cust1@example.com",
            Phone: "9876543210",
            Address: "123 St",
            City: "Pune",
            State: "Maharashtra",
            Status: CustomerStatus.Active,
            OwnerId: users.ExecS1Id, // Trying to assign to south executive
            Notes: "Some note");

        var result = await service.CreateAsync(cmd);
        result.Succeeded.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value.OwnerId.ShouldBe(users.ExecN1Id); // Forced to caller
    }

    [Fact]
    public async Task Create_as_manager_can_assign_team_member()
    {
        await using var ctx = Db.CreateContext();
        var users = await TestUsers.CreateStandardAsync(ctx);

        using var harness = ServiceHarness.Create(Db, users.NorthManagerId, Roles.Manager);
        var service = harness.Services.GetRequiredService<ICustomerService>();

        var cmd = new CreateCustomerCommand(
            Name: "Customer Two",
            CompanyName: "Acme",
            Email: "cust2@example.com",
            Phone: "9876543211",
            Address: null,
            City: null,
            State: null,
            Status: CustomerStatus.Active,
            OwnerId: users.ExecN1Id, // In team
            Notes: null);

        var result = await service.CreateAsync(cmd);
        result.Succeeded.ShouldBeTrue();
        result.Value!.OwnerId.ShouldBe(users.ExecN1Id);
    }

    [Fact]
    public async Task Create_as_manager_cannot_assign_other_team_member()
    {
        await using var ctx = Db.CreateContext();
        var users = await TestUsers.CreateStandardAsync(ctx);

        using var harness = ServiceHarness.Create(Db, users.NorthManagerId, Roles.Manager);
        var service = harness.Services.GetRequiredService<ICustomerService>();

        var cmd = new CreateCustomerCommand(
            Name: "Customer Three",
            CompanyName: "Acme",
            Email: "cust3@example.com",
            Phone: "9876543212",
            Address: null,
            City: null,
            State: null,
            Status: CustomerStatus.Active,
            OwnerId: users.ExecS1Id, // In south team, not north
            Notes: null);

        var result = await service.CreateAsync(cmd);
        result.Succeeded.ShouldBeFalse();
        result.Kind.ShouldBe(ErrorKind.Validation);
        result.Errors.ShouldContain(e => e.Field == "OwnerId");
    }

    [Fact]
    public async Task Create_as_executive_cannot_assign_other()
    {
        await using var ctx = Db.CreateContext();
        var users = await TestUsers.CreateStandardAsync(ctx);

        using var harness = ServiceHarness.Create(Db, users.ExecN1Id, Roles.SalesExecutive);
        var service = harness.Services.GetRequiredService<ICustomerService>();

        var cmd = new CreateCustomerCommand(
            Name: "Customer Four",
            CompanyName: null,
            Email: "cust4@example.com",
            Phone: "9876543213",
            Address: null,
            City: null,
            State: null,
            Status: CustomerStatus.Active,
            OwnerId: users.AdminId, // Invalid request
            Notes: null);

        var result = await service.CreateAsync(cmd);
        result.Succeeded.ShouldBeTrue();
        result.Value!.OwnerId.ShouldBe(users.ExecN1Id); // Owner was forced to self
    }

    [Fact]
    public async Task Create_normalises_phone_and_email_and_assigns_code()
    {
        await using var ctx = Db.CreateContext();
        var users = await TestUsers.CreateStandardAsync(ctx);

        using var harness = ServiceHarness.Create(Db, users.ExecN1Id, Roles.SalesExecutive);
        var service = harness.Services.GetRequiredService<ICustomerService>();

        var cmd = new CreateCustomerCommand(
            Name: "  Padded Name  ",
            CompanyName: "  Acme Corp  ",
            Email: "  User@Example.COM  ",
            Phone: "+91 98765 43210",
            Address: "  Some Addr  ",
            City: "  Mumbai  ",
            State: "  MH  ",
            Status: CustomerStatus.Active,
            OwnerId: null,
            Notes: "  Note  ");

        var result = await service.CreateAsync(cmd);
        result.Succeeded.ShouldBeTrue();
        var c = result.Value!;
        c.Name.ShouldBe("Padded Name");
        c.CompanyName.ShouldBe("Acme Corp");
        c.Email.ShouldBe("User@Example.COM");
        c.Phone.ShouldBe("9876543210");
        c.Address.ShouldBe("Some Addr");
        c.City.ShouldBe("Mumbai");
        c.State.ShouldBe("MH");
        c.Notes.ShouldBe("Note");
        c.Code.ShouldStartWith("CUS-");
        c.Code.Length.ShouldBe(10);
    }

    [Fact]
    public async Task Update_changes_fields_and_stamps_ModifiedBy()
    {
        await using var ctx = Db.CreateContext();
        var users = await TestUsers.CreateStandardAsync(ctx);

        using var harness = ServiceHarness.Create(Db, users.ExecN1Id, Roles.SalesExecutive);
        var service = harness.Services.GetRequiredService<ICustomerService>();

        var createResult = await service.CreateAsync(new CreateCustomerCommand(
            "Original Name", null, "orig@example.com", "9876543210", null, null, null, CustomerStatus.Active, null, null));
        createResult.Succeeded.ShouldBeTrue();
        var created = createResult.Value!;

        var updateResult = await service.UpdateAsync(created.Id, new UpdateCustomerCommand(
            Name: "Updated Name",
            CompanyName: "New Company",
            Email: "updated@example.com",
            Phone: "9876543211",
            Address: "New Addr",
            City: "New City",
            State: "New State",
            Status: CustomerStatus.Inactive,
            OwnerId: null,
            Notes: "Updated Notes",
            RowVersion: created.RowVersion));

        updateResult.Succeeded.ShouldBeTrue();
        var updated = updateResult.Value!;
        updated.Name.ShouldBe("Updated Name");
        updated.CompanyName.ShouldBe("New Company");
        updated.Email.ShouldBe("updated@example.com");
        updated.Phone.ShouldBe("9876543211");
        updated.Status.ShouldBe(CustomerStatus.Inactive);
        updated.ModifiedDate.ShouldNotBeNull();

        await using var verifyCtx = Db.CreateContext();
        var dbEntity = await verifyCtx.Customers.FindAsync(created.Id);
        dbEntity!.ModifiedBy.ShouldBe(users.ExecN1Id);
    }

    [Fact]
    public async Task Update_with_stale_RowVersion_returns_Conflict()
    {
        await using var ctx = Db.CreateContext();
        var users = await TestUsers.CreateStandardAsync(ctx);

        using var harness = ServiceHarness.Create(Db, users.ExecN1Id, Roles.SalesExecutive);
        var service = harness.Services.GetRequiredService<ICustomerService>();

        var createResult = await service.CreateAsync(new CreateCustomerCommand(
            "Customer Name", null, "conflict@example.com", "9876543210", null, null, null, CustomerStatus.Active, null, null));
        createResult.Succeeded.ShouldBeTrue();
        var created = createResult.Value!;

        // First update succeeds and changes RowVersion
        var u1 = await service.UpdateAsync(created.Id, new UpdateCustomerCommand(
            "Updated Once", null, "conflict@example.com", "9876543210", null, null, null, CustomerStatus.Active, null, null, created.RowVersion));
        u1.Succeeded.ShouldBeTrue();

        // Second update uses original stale RowVersion
        var u2 = await service.UpdateAsync(created.Id, new UpdateCustomerCommand(
            "Updated Twice", null, "conflict@example.com", "9876543210", null, null, null, CustomerStatus.Active, null, null, created.RowVersion));

        u2.Succeeded.ShouldBeFalse();
        u2.Kind.ShouldBe(ErrorKind.Conflict);
        u2.Errors.ShouldContain(e => e.Message == "This customer was changed by someone else. Reload and try again.");
    }

    [Fact]
    public async Task Delete_soft_deletes_and_hides_from_search()
    {
        await using var ctx = Db.CreateContext();
        var users = await TestUsers.CreateStandardAsync(ctx);

        using var harness = ServiceHarness.Create(Db, users.AdminId, Roles.Admin);
        var service = harness.Services.GetRequiredService<ICustomerService>();

        var createResult = await service.CreateAsync(new CreateCustomerCommand(
            "To Delete", null, "todelete@example.com", "9876543210", null, null, null, CustomerStatus.Active, null, null));
        createResult.Succeeded.ShouldBeTrue();
        var id = createResult.Value!.Id;

        var delResult = await service.DeleteAsync(id);
        delResult.Succeeded.ShouldBeTrue();

        // Search should not return soft deleted customer
        var search = await service.SearchAsync(new CustomerFilter(Search: "To Delete"), new PageQuery());
        search.Items.ShouldNotContain(c => c.Id == id);
        search.Total.ShouldBe(0);

        // Get should return NotFound
        var get = await service.GetAsync(id);
        get.Succeeded.ShouldBeFalse();
        get.Kind.ShouldBe(ErrorKind.NotFound);

        // In DB IsDeleted is 1
        await using var verifyCtx = Db.CreateContext();
        var raw = await verifyCtx.Customers.IgnoreQueryFilters().FirstOrDefaultAsync(c => c.Id == id);
        raw.ShouldNotBeNull();
        raw.IsDeleted.ShouldBeTrue();
        raw.DeletedBy.ShouldBe(users.AdminId);
        raw.DeletedDate.ShouldNotBeNull();
    }

    [Fact]
    public async Task Search_by_name_email_phone_company_is_case_insensitive()
    {
        await using var ctx = Db.CreateContext();
        var users = await TestUsers.CreateStandardAsync(ctx);

        using var harness = ServiceHarness.Create(Db, users.AdminId, Roles.Admin);
        var service = harness.Services.GetRequiredService<ICustomerService>();

        await service.CreateAsync(new CreateCustomerCommand(
            "Rajesh Sharma", "Tata Consultancy", "rajesh.s@tata.com", "9876543210", null, null, null, CustomerStatus.Active, null, null));

        // Name case-insensitive
        var byName = await service.SearchAsync(new CustomerFilter(Search: "RAJESH"), new PageQuery());
        byName.Total.ShouldBe(1);

        // Company case-insensitive
        var byCompany = await service.SearchAsync(new CustomerFilter(Search: "tata"), new PageQuery());
        byCompany.Total.ShouldBe(1);

        // Email case-insensitive
        var byEmail = await service.SearchAsync(new CustomerFilter(Search: "RAJESH.S@TATA"), new PageQuery());
        byEmail.Total.ShouldBe(1);

        // Phone
        var byPhone = await service.SearchAsync(new CustomerFilter(Search: "987654"), new PageQuery());
        byPhone.Total.ShouldBe(1);
    }

    [Fact]
    public async Task Search_filters_status_owner()
    {
        await using var ctx = Db.CreateContext();
        var users = await TestUsers.CreateStandardAsync(ctx);

        using var harness = ServiceHarness.Create(Db, users.AdminId, Roles.Admin);
        var service = harness.Services.GetRequiredService<ICustomerService>();

        await service.CreateAsync(new CreateCustomerCommand(
            "Active ExecN1", null, "act1@example.com", "9876543210", null, null, null, CustomerStatus.Active, users.ExecN1Id, null));
        await service.CreateAsync(new CreateCustomerCommand(
            "Inactive ExecN1", null, "inact1@example.com", "9876543211", null, null, null, CustomerStatus.Inactive, users.ExecN1Id, null));
        await service.CreateAsync(new CreateCustomerCommand(
            "Active ExecS1", null, "act2@example.com", "9876543212", null, null, null, CustomerStatus.Active, users.ExecS1Id, null));

        // Filter by Status Active
        var activeOnly = await service.SearchAsync(new CustomerFilter(Status: CustomerStatus.Active), new PageQuery());
        activeOnly.Total.ShouldBe(2);

        // Filter by Owner ExecN1
        var execN1Only = await service.SearchAsync(new CustomerFilter(OwnerId: users.ExecN1Id), new PageQuery());
        execN1Only.Total.ShouldBe(2);

        // Filter by both
        var both = await service.SearchAsync(new CustomerFilter(Status: CustomerStatus.Active, OwnerId: users.ExecN1Id), new PageQuery());
        both.Total.ShouldBe(1);
        both.Items[0].Name.ShouldBe("Active ExecN1");
    }

    [Fact]
    public async Task Search_sorting_and_paging()
    {
        await using var ctx = Db.CreateContext();
        var users = await TestUsers.CreateStandardAsync(ctx);

        using var harness = ServiceHarness.Create(Db, users.AdminId, Roles.Admin);
        var service = harness.Services.GetRequiredService<ICustomerService>();

        for (int i = 1; i <= 30; i++)
        {
            await service.CreateAsync(new CreateCustomerCommand(
                $"Customer {i:D2}", null, $"cust{i:D2}@example.com", $"98765{i:D5}", null, null, null, CustomerStatus.Active, null, null));
        }

        // Page 1 with pageSize 10
        var p1 = await service.SearchAsync(new CustomerFilter(), new PageQuery(Page: 1, PageSize: 10, Sort: "name", Desc: false));
        p1.Items.Count.ShouldBe(10);
        p1.Total.ShouldBe(30);
        p1.Items[0].Name.ShouldBe("Customer 01");
        p1.Items[9].Name.ShouldBe("Customer 10");

        // Page 2
        var p2 = await service.SearchAsync(new CustomerFilter(), new PageQuery(Page: 2, PageSize: 10, Sort: "name", Desc: false));
        p2.Items.Count.ShouldBe(10);
        p2.Items[0].Name.ShouldBe("Customer 11");

        // Sort descending
        var pDesc = await service.SearchAsync(new CustomerFilter(), new PageQuery(Page: 1, PageSize: 5, Sort: "name", Desc: true));
        pDesc.Items[0].Name.ShouldBe("Customer 30");
    }

    [Fact]
    public async Task Search_treats_LIKE_wildcards_literally()
    {
        await using var ctx = Db.CreateContext();
        var users = await TestUsers.CreateStandardAsync(ctx);

        using var harness = ServiceHarness.Create(Db, users.AdminId, Roles.Admin);
        var service = harness.Services.GetRequiredService<ICustomerService>();

        await service.CreateAsync(new CreateCustomerCommand(
            "100% Match", "Special [Group]", "test%_1@example.com", "9876543210", null, null, null, CustomerStatus.Active, null, null));
        await service.CreateAsync(new CreateCustomerCommand(
            "1000 Other", "Regular Group", "regular@example.com", "9876543211", null, null, null, CustomerStatus.Active, null, null));

        // Search '%' should match "100% Match" and "test%_1@example.com", but not all rows!
        var pct = await service.SearchAsync(new CustomerFilter(Search: "%"), new PageQuery());
        pct.Total.ShouldBe(1);
        pct.Items[0].Name.ShouldBe("100% Match");

        // Search '['
        var bracket = await service.SearchAsync(new CustomerFilter(Search: "[Group]"), new PageQuery());
        bracket.Total.ShouldBe(1);
        bracket.Items[0].Name.ShouldBe("100% Match");

        // SQL injection attempt
        var sqlInj = await service.SearchAsync(new CustomerFilter(Search: "' OR 1=1 --"), new PageQuery());
        sqlInj.Total.ShouldBe(0);
    }

    [Fact]
    public async Task Lookup_returns_only_active_in_scope()
    {
        await using var ctx = Db.CreateContext();
        var users = await TestUsers.CreateStandardAsync(ctx);

        using var harness = ServiceHarness.Create(Db, users.NorthManagerId, Roles.Manager);
        var service = harness.Services.GetRequiredService<ICustomerService>();

        // Active in team
        await service.CreateAsync(new CreateCustomerCommand(
            "Acme Active North", null, "acme.north@example.com", "9876543210", null, null, null, CustomerStatus.Active, users.ExecN1Id, null));

        // Inactive in team
        await service.CreateAsync(new CreateCustomerCommand(
            "Acme Inactive North", null, "acme.inact@example.com", "9876543211", null, null, null, CustomerStatus.Inactive, users.ExecN1Id, null));

        // Active in other team (created via admin)
        using (var adminHarness = ServiceHarness.Create(Db, users.AdminId, Roles.Admin))
        {
            var adminService = adminHarness.Services.GetRequiredService<ICustomerService>();
            await adminService.CreateAsync(new CreateCustomerCommand(
                "Acme South Active", null, "acme.south@example.com", "9876543212", null, null, null, CustomerStatus.Active, users.ExecS1Id, null));
        }

        var lookup = await service.LookupAsync("Acme");
        lookup.Count.ShouldBe(1);
        lookup[0].Name.ShouldBe("Acme Active North");
    }

    [Fact]
    public async Task Executive_without_permission_is_Forbidden()
    {
        await using var ctx = Db.CreateContext();
        var users = await TestUsers.CreateStandardAsync(ctx);

        var customer = Customer.Create("Exec Customer", null, "exec.cust@example.com", "9876543210", null, null, null, CustomerStatus.Active, users.ExecN1Id, null);
        ctx.Customers.Add(customer);
        await ctx.SaveChangesAsync();

        // 1. SalesExecutive trying to delete their own customer is Forbidden (sales executives do not have customers.delete)
        using (var execHarness = ServiceHarness.Create(Db, users.ExecN1Id, Roles.SalesExecutive))
        {
            var execService = execHarness.Services.GetRequiredService<ICustomerService>();
            var delete = await execService.DeleteAsync(customer.Id);
            delete.Succeeded.ShouldBeFalse();
            delete.Kind.ShouldBe(ErrorKind.Forbidden);
        }

        // 2. User with custom role without permissions is Forbidden on create
        using (var noPermHarness = ServiceHarness.Create(Db, users.ExecN1Id, "CustomNoPerms"))
        {
            var noPermService = noPermHarness.Services.GetRequiredService<ICustomerService>();
            var create = await noPermService.CreateAsync(new CreateCustomerCommand(
                "Name", null, "no.perm@example.com", "9876543210", null, null, null, CustomerStatus.Active, null, null));
            create.Succeeded.ShouldBeFalse();
            create.Kind.ShouldBe(ErrorKind.Forbidden);
        }
    }
}

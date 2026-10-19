using AcxiomCrm.Application.Activities;
using AcxiomCrm.Application.Common;
using AcxiomCrm.Domain.Entities;
using AcxiomCrm.Domain.Enums;
using AcxiomCrm.Domain.Security;
using AcxiomCrm.TestSupport;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace AcxiomCrm.Application.Tests;

public class ActivityServiceTests : DbTestBase
{
    private static readonly DateTime FixedDate = new(2026, 10, 19, 10, 0, 0, DateTimeKind.Utc);
    private static readonly DateOnly Today = new(2026, 10, 19);

    public ActivityServiceTests(SqlServerContainerFixture fixture) : base(fixture)
    {
    }

    [Fact]
    public async Task Create_valid_with_one_and_multiple_related_records()
    {
        await using var ctx = Db.CreateContext();
        var users = await TestUsers.CreateStandardAsync(ctx);

        var customer = Customer.Create("Act Cust One", null, "ac1@example.com", "9876543210", null, null, null, CustomerStatus.Active, users.ExecN1Id, null);
        ctx.Customers.Add(customer);
        await ctx.SaveChangesAsync();

        var opp = Opportunity.Create("Act Opp One", customer.Id, 25000m, OpportunityStage.Proposal, 40, Today.AddDays(5), users.ExecN1Id);
        ctx.Opportunities.Add(opp);
        await ctx.SaveChangesAsync();

        var clock = new FakeClock(FixedDate);
        using var harness = ServiceHarness.Create(Db, users.ExecN1Id, Roles.SalesExecutive, clock);
        var service = harness.Services.GetRequiredService<IActivityService>();

        // 1. Single related
        var cmd1 = new CreateActivityCommand(
            Type: ActivityType.Call,
            Subject: "Introductory Call",
            Description: "Discussed general requirements",
            ActivityDateUtc: FixedDate,
            Related: [new RelatedRef(RelatedType.Customer, customer.Id)],
            AssignedToId: users.ExecN1Id);

        var res1 = await service.CreateAsync(cmd1);
        res1.Succeeded.ShouldBeTrue();
        res1.Value!.CustomerId.ShouldBe(customer.Id);
        res1.Value.CustomerName.ShouldBe("Act Cust One");
        res1.Value.LeadId.ShouldBeNull();
        res1.Value.OpportunityId.ShouldBeNull();
        res1.Value.Status.ShouldBe(ActivityStatus.Completed);

        // 2. Multiple related (Customer + Opportunity)
        var cmd2 = new CreateActivityCommand(
            Type: ActivityType.Meeting,
            Subject: "Demo Meeting",
            Description: "Presented product proposal",
            ActivityDateUtc: FixedDate.AddHours(2),
            Related: [
                new RelatedRef(RelatedType.Customer, customer.Id),
                new RelatedRef(RelatedType.Opportunity, opp.Id)
            ],
            AssignedToId: users.ExecN1Id);

        var res2 = await service.CreateAsync(cmd2);
        res2.Succeeded.ShouldBeTrue();
        res2.Value!.CustomerId.ShouldBe(customer.Id);
        res2.Value.OpportunityId.ShouldBe(opp.Id);
        res2.Value.OpportunityName.ShouldBe("Act Opp One");
    }

    [Fact]
    public async Task Create_with_duplicate_related_type_or_empty_returns_validation_error()
    {
        await using var ctx = Db.CreateContext();
        var users = await TestUsers.CreateStandardAsync(ctx);

        var customer = Customer.Create("Act Cust Dup", null, "acdup@example.com", "9876543211", null, null, null, CustomerStatus.Active, users.ExecN1Id, null);
        ctx.Customers.Add(customer);
        await ctx.SaveChangesAsync();

        using var harness = ServiceHarness.Create(Db, users.ExecN1Id, Roles.SalesExecutive);
        var service = harness.Services.GetRequiredService<IActivityService>();

        // Empty
        var emptyCmd = new CreateActivityCommand(
            Type: ActivityType.Call,
            Subject: "Empty Call",
            Description: null,
            ActivityDateUtc: FixedDate,
            Related: [],
            AssignedToId: users.ExecN1Id);

        var emptyRes = await service.CreateAsync(emptyCmd);
        emptyRes.Succeeded.ShouldBeFalse();
        emptyRes.Kind.ShouldBe(ErrorKind.Validation);

        // Duplicate type
        var dupCmd = new CreateActivityCommand(
            Type: ActivityType.Call,
            Subject: "Dup Call",
            Description: null,
            ActivityDateUtc: FixedDate,
            Related: [
                new RelatedRef(RelatedType.Customer, customer.Id),
                new RelatedRef(RelatedType.Customer, customer.Id)
            ],
            AssignedToId: users.ExecN1Id);

        var dupRes = await service.CreateAsync(dupCmd);
        dupRes.Succeeded.ShouldBeFalse();
        dupRes.Kind.ShouldBe(ErrorKind.Validation);
    }

    [Fact]
    public async Task Create_with_out_of_scope_related_record_returns_validation_error()
    {
        await using var ctx = Db.CreateContext();
        var users = await TestUsers.CreateStandardAsync(ctx);

        // Belongs to South executive
        var customerSouth = Customer.Create("South Cust", null, "actsouth@example.com", "9876543212", null, null, null, CustomerStatus.Active, users.ExecS1Id, null);
        ctx.Customers.Add(customerSouth);
        await ctx.SaveChangesAsync();

        // North executive attempts to link
        using var harness = ServiceHarness.Create(Db, users.ExecN1Id, Roles.SalesExecutive);
        var service = harness.Services.GetRequiredService<IActivityService>();

        var cmd = new CreateActivityCommand(
            Type: ActivityType.Email,
            Subject: "Out of scope email",
            Description: null,
            ActivityDateUtc: FixedDate,
            Related: [new RelatedRef(RelatedType.Customer, customerSouth.Id)],
            AssignedToId: users.ExecN1Id);

        var res = await service.CreateAsync(cmd);
        res.Succeeded.ShouldBeFalse();
        res.Kind.ShouldBe(ErrorKind.Validation);
        res.Errors.ShouldContain(e => e.Field == "Related");
    }

    [Fact]
    public async Task Update_and_delete_valid()
    {
        await using var ctx = Db.CreateContext();
        var users = await TestUsers.CreateStandardAsync(ctx);

        var customer = Customer.Create("Act Cust Upd", null, "acupd@example.com", "9876543213", null, null, null, CustomerStatus.Active, users.ExecN1Id, null);
        ctx.Customers.Add(customer);
        await ctx.SaveChangesAsync();

        var clock = new FakeClock(FixedDate);
        using var harness = ServiceHarness.Create(Db, users.ExecN1Id, Roles.SalesExecutive, clock);
        var service = harness.Services.GetRequiredService<IActivityService>();

        var createRes = await service.CreateAsync(new CreateActivityCommand(
            Type: ActivityType.Task,
            Subject: "Initial Task",
            Description: "Initial note",
            ActivityDateUtc: FixedDate,
            Related: [new RelatedRef(RelatedType.Customer, customer.Id)],
            AssignedToId: users.ExecN1Id));

        createRes.Succeeded.ShouldBeTrue();

        // Update
        var updateRes = await service.UpdateAsync(createRes.Value!.Id, new UpdateActivityCommand(
            Type: ActivityType.Meeting,
            Subject: "Updated Meeting",
            Description: "Updated notes",
            ActivityDateUtc: FixedDate.AddDays(1),
            Related: [new RelatedRef(RelatedType.Customer, customer.Id)],
            AssignedToId: users.ExecN1Id,
            Status: ActivityStatus.Completed,
            RowVersion: createRes.Value.RowVersion));

        updateRes.Succeeded.ShouldBeTrue();
        updateRes.Value!.Subject.ShouldBe("Updated Meeting");
        updateRes.Value.Type.ShouldBe(ActivityType.Meeting);

        // Delete
        var delRes = await service.DeleteAsync(createRes.Value.Id);
        delRes.Succeeded.ShouldBeTrue();

        // Should now be NotFound
        var getRes = await service.GetAsync(createRes.Value.Id);
        getRes.Succeeded.ShouldBeFalse();
        getRes.Kind.ShouldBe(ErrorKind.NotFound);
    }

    [Fact]
    public async Task Search_filters_by_type_status_and_assignee()
    {
        await using var ctx = Db.CreateContext();
        var users = await TestUsers.CreateStandardAsync(ctx);

        var customer = Customer.Create("Act Search Cust", null, "acsrch@example.com", "9876543214", null, null, null, CustomerStatus.Active, users.ExecN1Id, null);
        ctx.Customers.Add(customer);
        await ctx.SaveChangesAsync();

        var clock = new FakeClock(FixedDate);
        using var harness = ServiceHarness.Create(Db, users.NorthManagerId, Roles.Manager, clock);
        var service = harness.Services.GetRequiredService<IActivityService>();

        await service.CreateAsync(new CreateActivityCommand(
            Type: ActivityType.Call,
            Subject: "Call 1",
            Description: null,
            ActivityDateUtc: FixedDate,
            Related: [new RelatedRef(RelatedType.Customer, customer.Id)],
            AssignedToId: users.ExecN1Id,
            Status: ActivityStatus.Completed));

        await service.CreateAsync(new CreateActivityCommand(
            Type: ActivityType.Email,
            Subject: "Email 1",
            Description: null,
            ActivityDateUtc: FixedDate.AddDays(1),
            Related: [new RelatedRef(RelatedType.Customer, customer.Id)],
            AssignedToId: users.NorthManagerId,
            Status: ActivityStatus.Planned));

        // Filter by Type
        var searchType = await service.SearchAsync(new ActivityFilter(Type: ActivityType.Call), new PageQuery(1, 10));
        searchType.Total.ShouldBe(1);
        searchType.Items[0].Subject.ShouldBe("Call 1");

        // Filter by Status
        var searchStatus = await service.SearchAsync(new ActivityFilter(Status: ActivityStatus.Planned), new PageQuery(1, 10));
        searchStatus.Total.ShouldBe(1);
        searchStatus.Items[0].Subject.ShouldBe("Email 1");

        // Filter by Assignee
        var searchAssignee = await service.SearchAsync(new ActivityFilter(AssignedToId: users.NorthManagerId), new PageQuery(1, 10));
        searchAssignee.Total.ShouldBe(1);
        searchAssignee.Items[0].Subject.ShouldBe("Email 1");
    }

    [Fact]
    public async Task IDOR_other_team_activity_not_found()
    {
        await using var ctx = Db.CreateContext();
        var users = await TestUsers.CreateStandardAsync(ctx);

        var customerSouth = Customer.Create("South Cust IDOR", null, "sidor@example.com", "9876543215", null, null, null, CustomerStatus.Active, users.ExecS1Id, null);
        ctx.Customers.Add(customerSouth);
        await ctx.SaveChangesAsync();

        using var southHarness = ServiceHarness.Create(Db, users.ExecS1Id, Roles.SalesExecutive);
        var southService = southHarness.Services.GetRequiredService<IActivityService>();

        var actRes = await southService.CreateAsync(new CreateActivityCommand(
            Type: ActivityType.Call,
            Subject: "South Secret Call",
            Description: null,
            ActivityDateUtc: FixedDate,
            Related: [new RelatedRef(RelatedType.Customer, customerSouth.Id)],
            AssignedToId: users.ExecS1Id));

        actRes.Succeeded.ShouldBeTrue();

        // North executive attempts to access
        using var northHarness = ServiceHarness.Create(Db, users.ExecN1Id, Roles.SalesExecutive);
        var northService = northHarness.Services.GetRequiredService<IActivityService>();

        var getRes = await northService.GetAsync(actRes.Value!.Id);
        getRes.Succeeded.ShouldBeFalse();
        getRes.Kind.ShouldBe(ErrorKind.NotFound);
    }
}

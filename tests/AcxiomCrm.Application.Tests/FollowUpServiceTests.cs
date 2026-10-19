using AcxiomCrm.Application.Common;
using AcxiomCrm.Application.FollowUps;
using AcxiomCrm.Domain.Entities;
using AcxiomCrm.Domain.Enums;
using AcxiomCrm.Domain.Security;
using AcxiomCrm.Domain.Validation;
using AcxiomCrm.TestSupport;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace AcxiomCrm.Application.Tests;

public class FollowUpServiceTests : DbTestBase
{
    private static readonly DateTime FixedDate = new(2026, 10, 19, 10, 0, 0, DateTimeKind.Utc);
    private static readonly DateOnly Today = new(2026, 10, 19);

    public FollowUpServiceTests(SqlServerContainerFixture fixture) : base(fixture)
    {
    }

    [Fact]
    public async Task Create_valid_against_customer_lead_and_opportunity()
    {
        await using var ctx = Db.CreateContext();
        var users = await TestUsers.CreateStandardAsync(ctx);

        var customer = Customer.Create("Cust One", null, "c1@example.com", "9876543210", null, null, null, CustomerStatus.Active, users.ExecN1Id, null);
        var lead = Lead.Create("Lead One", null, "l1@example.com", "9876543211", LeadSource.Website, LeadPriority.Medium, 1000m, users.ExecN1Id, null);
        ctx.Customers.Add(customer);
        ctx.Leads.Add(lead);
        await ctx.SaveChangesAsync();

        var opp = Opportunity.Create("Opp One", customer.Id, 50000m, OpportunityStage.Proposal, 50, Today.AddDays(10), users.ExecN1Id);
        ctx.Opportunities.Add(opp);
        await ctx.SaveChangesAsync();

        var clock = new FakeClock(FixedDate);
        using var harness = ServiceHarness.Create(Db, users.ExecN1Id, Roles.SalesExecutive, clock);
        var service = harness.Services.GetRequiredService<IFollowUpService>();

        // 1. Customer follow-up
        var cmd1 = new CreateFollowUpCommand(
            Related: new RelatedRef(RelatedType.Customer, customer.Id),
            FollowUpDateUtc: FixedDate.AddDays(1),
            Subject: "Call Customer",
            Type: FollowUpType.Call,
            Remarks: "Discuss quote",
            AssignedToId: users.ExecN1Id);

        var res1 = await service.CreateAsync(cmd1);
        res1.Succeeded.ShouldBeTrue();
        res1.Value!.RelatedType.ShouldBe(RelatedType.Customer);
        res1.Value.RelatedId.ShouldBe(customer.Id);
        res1.Value.RelatedName.ShouldBe("Cust One");
        res1.Value.Status.ShouldBe(FollowUpStatus.Planned);
        res1.Value.RescheduleCount.ShouldBe(0);

        // 2. Lead follow-up
        var cmd2 = new CreateFollowUpCommand(
            Related: new RelatedRef(RelatedType.Lead, lead.Id),
            FollowUpDateUtc: FixedDate.AddDays(2),
            Subject: "Email Lead",
            Type: FollowUpType.Email,
            Remarks: "Send demo deck",
            AssignedToId: users.ExecN1Id);

        var res2 = await service.CreateAsync(cmd2);
        res2.Succeeded.ShouldBeTrue();
        res2.Value!.RelatedType.ShouldBe(RelatedType.Lead);
        res2.Value.RelatedId.ShouldBe(lead.Id);
        res2.Value.RelatedName.ShouldBe("Lead One");

        // 3. Opportunity follow-up
        var cmd3 = new CreateFollowUpCommand(
            Related: new RelatedRef(RelatedType.Opportunity, opp.Id),
            FollowUpDateUtc: FixedDate.AddDays(3),
            Subject: "Meeting Opportunity",
            Type: FollowUpType.Meeting,
            Remarks: "Negotiation meeting",
            AssignedToId: users.ExecN1Id);

        var res3 = await service.CreateAsync(cmd3);
        res3.Succeeded.ShouldBeTrue();
        res3.Value!.RelatedType.ShouldBe(RelatedType.Opportunity);
        res3.Value.RelatedId.ShouldBe(opp.Id);
        res3.Value.RelatedName.ShouldBe("Opp One");
    }

    [Fact]
    public async Task Create_out_of_scope_related_record_returns_validation_error()
    {
        await using var ctx = Db.CreateContext();
        var users = await TestUsers.CreateStandardAsync(ctx);

        // Customer belongs to South team
        var customerSouth = Customer.Create("South Cust", null, "south@example.com", "9876543212", null, null, null, CustomerStatus.Active, users.ExecS1Id, null);
        ctx.Customers.Add(customerSouth);
        await ctx.SaveChangesAsync();

        var clock = new FakeClock(FixedDate);
        // ExecN1 is from North team
        using var harness = ServiceHarness.Create(Db, users.ExecN1Id, Roles.SalesExecutive, clock);
        var service = harness.Services.GetRequiredService<IFollowUpService>();

        var cmd = new CreateFollowUpCommand(
            Related: new RelatedRef(RelatedType.Customer, customerSouth.Id),
            FollowUpDateUtc: FixedDate.AddDays(1),
            Subject: "Sneak call",
            Type: FollowUpType.Call,
            Remarks: null,
            AssignedToId: users.ExecN1Id);

        var res = await service.CreateAsync(cmd);
        res.Succeeded.ShouldBeFalse();
        res.Kind.ShouldBe(ErrorKind.Validation);
        res.Errors.ShouldContain(e => e.Field == "Related");
    }

    [Fact]
    public async Task Create_executive_forced_to_own_assignment()
    {
        await using var ctx = Db.CreateContext();
        var users = await TestUsers.CreateStandardAsync(ctx);

        var customer = Customer.Create("Cust One", null, "c11@example.com", "9876543210", null, null, null, CustomerStatus.Active, users.ExecN1Id, null);
        ctx.Customers.Add(customer);
        await ctx.SaveChangesAsync();

        var clock = new FakeClock(FixedDate);
        using var harness = ServiceHarness.Create(Db, users.ExecN1Id, Roles.SalesExecutive, clock);
        var service = harness.Services.GetRequiredService<IFollowUpService>();

        // Executive tries to assign to someone else (NorthManager)
        var cmd = new CreateFollowUpCommand(
            Related: new RelatedRef(RelatedType.Customer, customer.Id),
            FollowUpDateUtc: FixedDate.AddDays(1),
            Subject: "Call",
            Type: FollowUpType.Call,
            Remarks: null,
            AssignedToId: users.NorthManagerId);

        var res = await service.CreateAsync(cmd);
        res.Succeeded.ShouldBeTrue();
        // Overridden to own ID
        res.Value!.AssignedToId.ShouldBe(users.ExecN1Id);
    }

    [Fact]
    public async Task Create_date_earlier_than_today_rejected_unless_historical()
    {
        await using var ctx = Db.CreateContext();
        var users = await TestUsers.CreateStandardAsync(ctx);

        var customer = Customer.Create("Cust Past", null, "cp@example.com", "9876543210", null, null, null, CustomerStatus.Active, users.ExecN1Id, null);
        ctx.Customers.Add(customer);
        await ctx.SaveChangesAsync();

        var clock = new FakeClock(FixedDate);
        using var harness = ServiceHarness.Create(Db, users.ExecN1Id, Roles.SalesExecutive, clock);
        var service = harness.Services.GetRequiredService<IFollowUpService>();

        // 1. Executive tries past date with isHistorical = false
        var cmdPast = new CreateFollowUpCommand(
            Related: new RelatedRef(RelatedType.Customer, customer.Id),
            FollowUpDateUtc: FixedDate.AddDays(-1),
            Subject: "Past call",
            Type: FollowUpType.Call,
            Remarks: null,
            AssignedToId: users.ExecN1Id,
            IsHistoricalEntry: false);

        var resPast = await service.CreateAsync(cmdPast);
        resPast.Succeeded.ShouldBeFalse();
        resPast.Errors.ShouldContain(e => e.Field == "FollowUpDate" && e.Message == Messages.FollowUpDateBeforeToday);

        // 2. Executive tries isHistorical = true (Forbidden)
        var cmdHistExec = new CreateFollowUpCommand(
            Related: new RelatedRef(RelatedType.Customer, customer.Id),
            FollowUpDateUtc: FixedDate.AddDays(-1),
            Subject: "Past call",
            Type: FollowUpType.Call,
            Remarks: null,
            AssignedToId: users.ExecN1Id,
            IsHistoricalEntry: true);

        var resHistExec = await service.CreateAsync(cmdHistExec);
        resHistExec.Succeeded.ShouldBeFalse();
        resHistExec.Kind.ShouldBe(ErrorKind.Forbidden);

        // 3. Manager tries isHistorical = true (Allowed)
        using var managerHarness = ServiceHarness.Create(Db, users.NorthManagerId, Roles.Manager, clock);
        var managerService = managerHarness.Services.GetRequiredService<IFollowUpService>();

        var resHistMgr = await managerService.CreateAsync(cmdHistExec);
        resHistMgr.Succeeded.ShouldBeTrue();
        resHistMgr.Value!.Status.ShouldBe(FollowUpStatus.Completed);
        resHistMgr.Value.IsHistoricalEntry.ShouldBeTrue();
    }

    [Fact]
    public async Task Complete_with_LogActivity_creates_activity_atomically()
    {
        await using var ctx = Db.CreateContext();
        var users = await TestUsers.CreateStandardAsync(ctx);

        var customer = Customer.Create("Cust LogAct", null, "cla@example.com", "9876543210", null, null, null, CustomerStatus.Active, users.ExecN1Id, null);
        ctx.Customers.Add(customer);
        await ctx.SaveChangesAsync();

        var clock = new FakeClock(FixedDate);
        using var harness = ServiceHarness.Create(Db, users.ExecN1Id, Roles.SalesExecutive, clock);
        var service = harness.Services.GetRequiredService<IFollowUpService>();

        var createRes = await service.CreateAsync(new CreateFollowUpCommand(
            new RelatedRef(RelatedType.Customer, customer.Id),
            FixedDate.AddDays(1),
            "Discovery Call",
            FollowUpType.Call,
            null,
            users.ExecN1Id));
        createRes.Succeeded.ShouldBeTrue();

        var completeRes = await service.CompleteAsync(createRes.Value!.Id, new CompleteFollowUpCommand(
            Remarks: "Client loved the proposal.",
            LogActivity: true,
            RowVersion: createRes.Value.RowVersion));

        completeRes.Succeeded.ShouldBeTrue();
        completeRes.Value!.Status.ShouldBe(FollowUpStatus.Completed);
        completeRes.Value.CompletedDate.ShouldBe(FixedDate);

        // Verify activity created in db
        await using var verifyCtx = Db.CreateContext();
        var activity = await verifyCtx.Activities.FirstOrDefaultAsync(a => a.CustomerId == customer.Id);
        activity.ShouldNotBeNull();
        activity.Subject.ShouldBe("Discovery Call");
        activity.Type.ShouldBe(ActivityType.Call);
        activity.Status.ShouldBe(ActivityStatus.Completed);
    }

    [Fact]
    public async Task Reschedule_increments_count_and_enforces_date_rule()
    {
        await using var ctx = Db.CreateContext();
        var users = await TestUsers.CreateStandardAsync(ctx);

        var customer = Customer.Create("Cust Resched", null, "cr@example.com", "9876543210", null, null, null, CustomerStatus.Active, users.ExecN1Id, null);
        ctx.Customers.Add(customer);
        await ctx.SaveChangesAsync();

        var clock = new FakeClock(FixedDate);
        using var harness = ServiceHarness.Create(Db, users.ExecN1Id, Roles.SalesExecutive, clock);
        var service = harness.Services.GetRequiredService<IFollowUpService>();

        var createRes = await service.CreateAsync(new CreateFollowUpCommand(
            new RelatedRef(RelatedType.Customer, customer.Id),
            FixedDate.AddDays(1),
            "Reschedule Test",
            FollowUpType.Meeting,
            null,
            users.ExecN1Id));

        // Reschedule to past date -> rejected
        var pastResched = await service.RescheduleAsync(createRes.Value!.Id, FixedDate.AddDays(-1), createRes.Value.RowVersion);
        pastResched.Succeeded.ShouldBeFalse();
        pastResched.Errors.ShouldContain(e => e.Field == "FollowUpDate" && e.Message == Messages.FollowUpDateBeforeToday);

        // Reschedule to future date -> OK
        var okResched = await service.RescheduleAsync(createRes.Value.Id, FixedDate.AddDays(5), createRes.Value.RowVersion);
        okResched.Succeeded.ShouldBeTrue();
        okResched.Value!.RescheduleCount.ShouldBe(1);
        okResched.Value.FollowUpDateUtc.ShouldBe(FixedDate.AddDays(5));
    }

    [Fact]
    public async Task MarkMissed_and_Cancel_work_for_planned_only()
    {
        await using var ctx = Db.CreateContext();
        var users = await TestUsers.CreateStandardAsync(ctx);

        var customer = Customer.Create("Cust MissedCancel", null, "cmc@example.com", "9876543210", null, null, null, CustomerStatus.Active, users.ExecN1Id, null);
        ctx.Customers.Add(customer);
        await ctx.SaveChangesAsync();

        var clock = new FakeClock(FixedDate);
        using var harness = ServiceHarness.Create(Db, users.ExecN1Id, Roles.SalesExecutive, clock);
        var service = harness.Services.GetRequiredService<IFollowUpService>();

        var item1 = await service.CreateAsync(new CreateFollowUpCommand(
            new RelatedRef(RelatedType.Customer, customer.Id),
            FixedDate.AddDays(1), "Item 1", FollowUpType.Call, null, users.ExecN1Id));

        var item2 = await service.CreateAsync(new CreateFollowUpCommand(
            new RelatedRef(RelatedType.Customer, customer.Id),
            FixedDate.AddDays(1), "Item 2", FollowUpType.Call, null, users.ExecN1Id));

        // Mark missed
        var missedRes = await service.MarkMissedAsync(item1.Value!.Id, item1.Value.RowVersion);
        missedRes.Succeeded.ShouldBeTrue();
        missedRes.Value!.Status.ShouldBe(FollowUpStatus.Missed);

        // Cancel
        var cancelRes = await service.CancelAsync(item2.Value!.Id, item2.Value.RowVersion);
        cancelRes.Succeeded.ShouldBeTrue();
        cancelRes.Value!.Status.ShouldBe(FollowUpStatus.Cancelled);

        // Transitioning again -> Conflict
        var conflictRes = await service.CompleteAsync(item1.Value.Id, new CompleteFollowUpCommand(null, false, missedRes.Value.RowVersion));
        conflictRes.Succeeded.ShouldBeFalse();
        conflictRes.Kind.ShouldBe(ErrorKind.Conflict);
    }

    [Fact]
    public async Task NotificationCounts_returns_caller_assigned_overdue_and_due_today()
    {
        await using var ctx = Db.CreateContext();
        var users = await TestUsers.CreateStandardAsync(ctx);

        var customer = Customer.Create("Cust Notif", null, "cn@example.com", "9876543210", null, null, null, CustomerStatus.Active, users.ExecN1Id, null);
        ctx.Customers.Add(customer);
        await ctx.SaveChangesAsync();

        var clock = new FakeClock(FixedDate);
        using var harness = ServiceHarness.Create(Db, users.ExecN1Id, Roles.SalesExecutive, clock);
        var service = harness.Services.GetRequiredService<IFollowUpService>();

        // Overdue (was planned earlier today or created as planned in past via DB)
        var pastFollowUp = FollowUp.Create(customer.Id, null, null, FixedDate.AddHours(-2), "Overdue item", FollowUpType.Call, null, users.ExecN1Id, false);
        var dueTodayFollowUp = FollowUp.Create(customer.Id, null, null, FixedDate.AddHours(2), "Due today item", FollowUpType.Call, null, users.ExecN1Id, false);
        var futureFollowUp = FollowUp.Create(customer.Id, null, null, FixedDate.AddDays(2), "Future item", FollowUpType.Call, null, users.ExecN1Id, false);
        var otherUserFollowUp = FollowUp.Create(customer.Id, null, null, FixedDate.AddHours(-1), "Other user overdue", FollowUpType.Call, null, users.ExecN2Id, false);

        ctx.FollowUps.AddRange(pastFollowUp, dueTodayFollowUp, futureFollowUp, otherUserFollowUp);
        await ctx.SaveChangesAsync();

        var counts = await service.GetNotificationCountsAsync();
        counts.Overdue.ShouldBe(1);
        counts.DueToday.ShouldBe(2); // both pastFollowUp (dated today) and dueTodayFollowUp (dated today) are due today!
    }

    [Fact]
    public async Task Delete_executive_allowed_for_own_planned_only()
    {
        await using var ctx = Db.CreateContext();
        var users = await TestUsers.CreateStandardAsync(ctx);

        var customer = Customer.Create("Cust Del", null, "cdel@example.com", "9876543210", null, null, null, CustomerStatus.Active, users.ExecN1Id, null);
        ctx.Customers.Add(customer);
        await ctx.SaveChangesAsync();

        var clock = new FakeClock(FixedDate);
        using var harness = ServiceHarness.Create(Db, users.ExecN1Id, Roles.SalesExecutive, clock);
        var service = harness.Services.GetRequiredService<IFollowUpService>();

        var plannedRes = await service.CreateAsync(new CreateFollowUpCommand(
            new RelatedRef(RelatedType.Customer, customer.Id),
            FixedDate.AddDays(1), "To Delete", FollowUpType.Call, null, users.ExecN1Id));

        var delRes = await service.DeleteAsync(plannedRes.Value!.Id);
        delRes.Succeeded.ShouldBeTrue();

        // Already completed
        var completed = FollowUp.Create(customer.Id, null, null, FixedDate, "Completed item", FollowUpType.Call, null, users.ExecN1Id, true);
        ctx.FollowUps.Add(completed);
        await ctx.SaveChangesAsync();

        var delCompletedRes = await service.DeleteAsync(completed.Id);
        delCompletedRes.Succeeded.ShouldBeFalse();
        delCompletedRes.Kind.ShouldBe(ErrorKind.Forbidden);
    }
}

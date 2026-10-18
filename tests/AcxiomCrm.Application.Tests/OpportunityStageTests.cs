using System.Globalization;
using AcxiomCrm.Application.Common;
using AcxiomCrm.Application.Opportunities;
using AcxiomCrm.Domain.Audit;
using AcxiomCrm.Domain.Entities;
using AcxiomCrm.Domain.Enums;
using AcxiomCrm.Domain.Security;
using AcxiomCrm.Domain.Validation;
using AcxiomCrm.TestSupport;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace AcxiomCrm.Application.Tests;

public class OpportunityStageTests : DbTestBase
{
    public OpportunityStageTests(SqlServerContainerFixture fixture) : base(fixture)
    {
    }

    [Theory]
    [InlineData(OpportunityStage.Qualification, OpportunityStage.Proposal)]
    [InlineData(OpportunityStage.Proposal, OpportunityStage.Negotiation)]
    [InlineData(OpportunityStage.Negotiation, OpportunityStage.Proposal)]
    [InlineData(OpportunityStage.Proposal, OpportunityStage.Qualification)]
    public async Task Open_to_open_stage_moves_succeed(OpportunityStage initialStage, OpportunityStage targetStage)
    {
        await using var ctx = Db.CreateContext();
        var users = await TestUsers.CreateStandardAsync(ctx);

        var customer = Customer.Create("Stage Cust", null, "stage@example.com", "9876543210", null, null, null, CustomerStatus.Active, users.AdminId, null);
        ctx.Customers.Add(customer);
        await ctx.SaveChangesAsync();

        using var harness = ServiceHarness.Create(Db, users.AdminId, Roles.Admin);
        var service = harness.Services.GetRequiredService<IOpportunityService>();

        var created = await service.CreateAsync(new CreateOpportunityCommand(
            "Move Deal", customer.Id, 10000m, 20, new DateOnly(2026, 12, 1),
            initialStage, null, users.AdminId, null));
        created.Succeeded.ShouldBeTrue();

        var moveResult = await service.ChangeStageAsync(created.Value!.Id, targetStage, null, created.Value.RowVersion);
        moveResult.Succeeded.ShouldBeTrue();
        moveResult.Value!.Stage.ShouldBe(targetStage);
        moveResult.Value.Status.ShouldBe(OpportunityStatus.Open);
    }

    [Fact]
    public async Task Move_to_Won_requires_note_and_sets_fields()
    {
        await using var ctx = Db.CreateContext();
        var users = await TestUsers.CreateStandardAsync(ctx);

        var customer = Customer.Create("Won Cust", null, "won@example.com", "9876543210", null, null, null, CustomerStatus.Active, users.AdminId, null);
        ctx.Customers.Add(customer);
        await ctx.SaveChangesAsync();

        var fixedTime = new DateTime(2026, 10, 16, 12, 0, 0, DateTimeKind.Utc);
        var clock = new FakeClock(fixedTime);
        using var harness = ServiceHarness.Create(Db, users.AdminId, Roles.Admin, clock);
        var service = harness.Services.GetRequiredService<IOpportunityService>();

        var created = await service.CreateAsync(new CreateOpportunityCommand(
            "Won Deal", customer.Id, 50000m, 50, new DateOnly(2026, 12, 1),
            OpportunityStage.Proposal, null, users.AdminId, null));
        created.Succeeded.ShouldBeTrue();

        // 1. Without note -> fails
        var failResult = await service.ChangeStageAsync(created.Value!.Id, OpportunityStage.Won, null, created.Value.RowVersion);
        failResult.Succeeded.ShouldBeFalse();
        failResult.Kind.ShouldBe(ErrorKind.Validation);
        failResult.Errors.ShouldContain(e => e.Field == "CloseNote" && e.Message == "Enter a note about why this opportunity was won.");

        // 2. With note -> succeeds
        var winResult = await service.ChangeStageAsync(created.Value.Id, OpportunityStage.Won, "Customer accepted terms and signed contract.", created.Value.RowVersion);
        winResult.Succeeded.ShouldBeTrue();
        winResult.Value!.Stage.ShouldBe(OpportunityStage.Won);
        winResult.Value.Status.ShouldBe(OpportunityStatus.Won);
        winResult.Value.Probability.ShouldBe(100);
        winResult.Value.ClosedDate.ShouldBe(clock.Today);
        winResult.Value.CloseNote.ShouldBe("Customer accepted terms and signed contract.");
    }

    [Fact]
    public async Task Move_to_Lost_requires_reason_and_sets_fields()
    {
        await using var ctx = Db.CreateContext();
        var users = await TestUsers.CreateStandardAsync(ctx);

        var customer = Customer.Create("Lost Cust", null, "lost@example.com", "9876543210", null, null, null, CustomerStatus.Active, users.AdminId, null);
        ctx.Customers.Add(customer);
        await ctx.SaveChangesAsync();

        var clock = new FakeClock(new DateTime(2026, 10, 16, 12, 0, 0, DateTimeKind.Utc));
        using var harness = ServiceHarness.Create(Db, users.AdminId, Roles.Admin, clock);
        var service = harness.Services.GetRequiredService<IOpportunityService>();

        var created = await service.CreateAsync(new CreateOpportunityCommand(
            "Lost Deal", customer.Id, 30000m, 50, new DateOnly(2026, 12, 1),
            OpportunityStage.Negotiation, null, users.AdminId, null));
        created.Succeeded.ShouldBeTrue();

        // 1. Without reason -> fails
        var failResult = await service.ChangeStageAsync(created.Value!.Id, OpportunityStage.Lost, "", created.Value.RowVersion);
        failResult.Succeeded.ShouldBeFalse();
        failResult.Kind.ShouldBe(ErrorKind.Validation);
        failResult.Errors.ShouldContain(e => e.Field == "CloseNote" && e.Message == "Enter the reason this opportunity was lost.");

        // 2. With reason -> succeeds
        var lostResult = await service.ChangeStageAsync(created.Value.Id, OpportunityStage.Lost, "Competitor offered lower pricing.", created.Value.RowVersion);
        lostResult.Succeeded.ShouldBeTrue();
        lostResult.Value!.Stage.ShouldBe(OpportunityStage.Lost);
        lostResult.Value.Status.ShouldBe(OpportunityStatus.Lost);
        lostResult.Value.Probability.ShouldBe(0);
        lostResult.Value.ClosedDate.ShouldBe(clock.Today);
        lostResult.Value.CloseNote.ShouldBe("Competitor offered lower pricing.");
    }

    [Fact]
    public async Task Closed_opportunity_stage_move_returns_conflict()
    {
        await using var ctx = Db.CreateContext();
        var users = await TestUsers.CreateStandardAsync(ctx);

        var customer = Customer.Create("Terminal Cust", null, "term@example.com", "9876543210", null, null, null, CustomerStatus.Active, users.AdminId, null);
        ctx.Customers.Add(customer);
        await ctx.SaveChangesAsync();

        var opp = Opportunity.Create(
            "Won Terminal Deal", customer.Id, 25000m, OpportunityStage.Won, 100,
            new DateOnly(2026, 10, 1), users.AdminId, closeNote: "Signed contract", closedDate: new DateOnly(2026, 10, 1));
        ctx.Opportunities.Add(opp);
        await ctx.SaveChangesAsync();

        using var harness = ServiceHarness.Create(Db, users.AdminId, Roles.Admin);
        var service = harness.Services.GetRequiredService<IOpportunityService>();

        var result = await service.ChangeStageAsync(opp.Id, OpportunityStage.Proposal, null, opp.RowVersion);
        result.Succeeded.ShouldBeFalse();
        result.Kind.ShouldBe(ErrorKind.Conflict);
        result.Errors[0].Message.ShouldContain("cannot change stage");
    }

    [Fact]
    public async Task Reopen_by_manager_succeeds_and_executive_is_forbidden()
    {
        await using var ctx = Db.CreateContext();
        var users = await TestUsers.CreateStandardAsync(ctx);

        var customer = Customer.Create("Reopen Cust", null, "reopen@example.com", "9876543210", null, null, null, CustomerStatus.Active, users.ExecN1Id, null);
        ctx.Customers.Add(customer);
        await ctx.SaveChangesAsync();

        var opp = Opportunity.Create(
            "To Reopen", customer.Id, 50000m, OpportunityStage.Lost, 0,
            new DateOnly(2026, 10, 1), users.ExecN1Id, closeNote: "Budget cuts", closedDate: new DateOnly(2026, 10, 1));
        ctx.Opportunities.Add(opp);
        await ctx.SaveChangesAsync();

        // 1. Executive tries to reopen -> Forbidden
        using (var execHarness = ServiceHarness.Create(Db, users.ExecN1Id, Roles.SalesExecutive))
        {
            var execService = execHarness.Services.GetRequiredService<IOpportunityService>();
            var execResult = await execService.ReopenAsync(opp.Id, opp.RowVersion);
            execResult.Succeeded.ShouldBeFalse();
            execResult.Kind.ShouldBe(ErrorKind.Forbidden);
        }

        // 2. Other team manager tries to reopen -> NotFound (out of scope)
        using (var southMgrHarness = ServiceHarness.Create(Db, users.SouthManagerId, Roles.Manager))
        {
            var southService = southMgrHarness.Services.GetRequiredService<IOpportunityService>();
            var southResult = await southService.ReopenAsync(opp.Id, opp.RowVersion);
            southResult.Succeeded.ShouldBeFalse();
            southResult.Kind.ShouldBe(ErrorKind.NotFound);
        }

        // 3. North Manager reopens -> Ok
        using (var northMgrHarness = ServiceHarness.Create(Db, users.NorthManagerId, Roles.Manager))
        {
            var northService = northMgrHarness.Services.GetRequiredService<IOpportunityService>();
            var reopenResult = await northService.ReopenAsync(opp.Id, opp.RowVersion);
            reopenResult.Succeeded.ShouldBeTrue();
            reopenResult.Value!.Stage.ShouldBe(OpportunityStage.Negotiation);
            reopenResult.Value.Status.ShouldBe(OpportunityStatus.Open);
            reopenResult.Value.Probability.ShouldBe(75);
            reopenResult.Value.ClosedDate.ShouldBeNull();
            reopenResult.Value.CloseNote.ShouldBeNull();
        }
    }

    [Fact]
    public async Task Backward_move_with_stale_close_date_rejected_until_updated()
    {
        await using var ctx = Db.CreateContext();
        var users = await TestUsers.CreateStandardAsync(ctx);

        var customer = Customer.Create("Stale Cust", null, "stale@example.com", "9876543210", null, null, null, CustomerStatus.Active, users.AdminId, null);
        ctx.Customers.Add(customer);
        await ctx.SaveChangesAsync();

        // Opportunity created with expected close date = 2026-10-15
        var opp = Opportunity.Create(
            "Stale Deal", customer.Id, 20000m, OpportunityStage.Proposal, 50,
            new DateOnly(2026, 10, 15), users.AdminId);
        ctx.Opportunities.Add(opp);
        await ctx.SaveChangesAsync();

        // Now today is 2026-10-16, so ExpectedCloseDate (2026-10-15) is now in the past
        var clock = new FakeClock(new DateTime(2026, 10, 16, 12, 0, 0, DateTimeKind.Utc));
        using var harness = ServiceHarness.Create(Db, users.AdminId, Roles.Admin, clock);
        var service = harness.Services.GetRequiredService<IOpportunityService>();

        var result = await service.ChangeStageAsync(opp.Id, OpportunityStage.Qualification, null, opp.RowVersion);
        result.Succeeded.ShouldBeFalse();
        result.Kind.ShouldBe(ErrorKind.Validation);
        result.Errors.ShouldContain(e => e.Field == "ExpectedCloseDate" && e.Message == Messages.ExpectedCloseDatePast);
    }

    [Fact]
    public async Task Stage_change_produces_StageChanged_audit_row()
    {
        await using var ctx = Db.CreateContext();
        var users = await TestUsers.CreateStandardAsync(ctx);

        var customer = Customer.Create("Audit Stage Cust", null, "auditstage@example.com", "9876543210", null, null, null, CustomerStatus.Active, users.AdminId, null);
        ctx.Customers.Add(customer);
        await ctx.SaveChangesAsync();

        using var harness = ServiceHarness.Create(Db, users.AdminId, Roles.Admin);
        var service = harness.Services.GetRequiredService<IOpportunityService>();

        var created = await service.CreateAsync(new CreateOpportunityCommand(
            "Audit Stage Deal", customer.Id, 10000m, 20, new DateOnly(2026, 12, 1),
            OpportunityStage.Qualification, null, users.AdminId, null));
        created.Succeeded.ShouldBeTrue();

        var moveResult = await service.ChangeStageAsync(created.Value!.Id, OpportunityStage.Proposal, null, created.Value.RowVersion);
        moveResult.Succeeded.ShouldBeTrue();

        await using var verifyCtx = Db.CreateContext();
        var audits = await verifyCtx.AuditLogs
            .Where(a => a.EntityName == nameof(Opportunity) && a.RecordId == created.Value.Id.ToString(CultureInfo.InvariantCulture))
            .ToListAsync();

        var stageAudit = audits.FirstOrDefault(a => a.Action == AuditActions.StageChanged);
        stageAudit.ShouldNotBeNull();
        stageAudit.Module.ShouldBe(AuditModules.Opportunities);
        stageAudit.OldValue.ShouldNotBeNull();
        stageAudit.NewValue.ShouldNotBeNull();
        stageAudit.OldValue.ShouldContain("Qualification");
        stageAudit.NewValue.ShouldContain("Proposal");
    }
}

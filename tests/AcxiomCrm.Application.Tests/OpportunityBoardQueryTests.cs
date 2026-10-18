using AcxiomCrm.Application.Opportunities;
using AcxiomCrm.Domain.Entities;
using AcxiomCrm.Domain.Enums;
using AcxiomCrm.Domain.Security;
using AcxiomCrm.TestSupport;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace AcxiomCrm.Application.Tests;

public class OpportunityBoardQueryTests : DbTestBase
{
    public OpportunityBoardQueryTests(SqlServerContainerFixture fixture) : base(fixture)
    {
    }

    [Fact]
    public async Task Columns_returned_in_five_stages_order()
    {
        await using var ctx = Db.CreateContext();
        var users = await TestUsers.CreateStandardAsync(ctx);

        using var harness = ServiceHarness.Create(Db, users.AdminId, Roles.Admin);
        var service = harness.Services.GetRequiredService<IOpportunityService>();

        var board = await service.GetBoardAsync(new OpportunityFilter());
        board.Count.ShouldBe(5);
        board[0].Stage.ShouldBe(OpportunityStage.Qualification);
        board[1].Stage.ShouldBe(OpportunityStage.Proposal);
        board[2].Stage.ShouldBe(OpportunityStage.Negotiation);
        board[3].Stage.ShouldBe(OpportunityStage.Won);
        board[4].Stage.ShouldBe(OpportunityStage.Lost);
    }

    [Fact]
    public async Task Totals_reflect_all_rows_even_when_cards_truncated_by_maxCardsPerColumn()
    {
        await using var ctx = Db.CreateContext();
        var users = await TestUsers.CreateStandardAsync(ctx);

        var customer = Customer.Create("Board Cust", null, "board@example.com", "9876543210", null, null, null, CustomerStatus.Active, users.AdminId, null);
        ctx.Customers.Add(customer);
        await ctx.SaveChangesAsync();

        // Create 3 opportunities in Proposal stage: 10000 (prob 50), 20000 (prob 50), 30000 (prob 50)
        // Total count = 3, Total amount = 60000, Total weighted = 30000
        var o1 = Opportunity.Create("Opp 1", customer.Id, 10000m, OpportunityStage.Proposal, 50, new DateOnly(2026, 12, 1), users.AdminId);
        var o2 = Opportunity.Create("Opp 2", customer.Id, 20000m, OpportunityStage.Proposal, 50, new DateOnly(2026, 12, 1), users.AdminId);
        var o3 = Opportunity.Create("Opp 3", customer.Id, 30000m, OpportunityStage.Proposal, 50, new DateOnly(2026, 12, 1), users.AdminId);
        ctx.Opportunities.AddRange(o1, o2, o3);
        await ctx.SaveChangesAsync();

        using var harness = ServiceHarness.Create(Db, users.AdminId, Roles.Admin);
        var service = harness.Services.GetRequiredService<IOpportunityService>();

        // Query with maxCardsPerColumn = 2
        var board = await service.GetBoardAsync(new OpportunityFilter(), maxCardsPerColumn: 2);
        var proposalCol = board.First(c => c.Stage == OpportunityStage.Proposal);

        proposalCol.Count.ShouldBe(3);
        proposalCol.Amount.ShouldBe(60000m);
        proposalCol.WeightedAmount.ShouldBe(30000m);
        proposalCol.Cards.Count.ShouldBe(2); // Truncated to 2 cards
    }

    [Fact]
    public async Task Closed_in_last_30_days_included_and_older_closed_excluded()
    {
        await using var ctx = Db.CreateContext();
        var users = await TestUsers.CreateStandardAsync(ctx);

        var customer = Customer.Create("Closed Cust", null, "closed30@example.com", "9876543210", null, null, null, CustomerStatus.Active, users.AdminId, null);
        ctx.Customers.Add(customer);
        await ctx.SaveChangesAsync();

        var today = new DateOnly(2026, 10, 16);
        var clock = new FakeClock(new DateTime(2026, 10, 16, 12, 0, 0, DateTimeKind.Utc));

        // Won 10 days ago (within 30 days)
        var recentWon = Opportunity.Create("Recent Won", customer.Id, 40000m, OpportunityStage.Won, 100,
            today.AddDays(-10), users.AdminId, closeNote: "Won recently", closedDate: today.AddDays(-10));

        // Won 45 days ago (older than 30 days)
        var oldWon = Opportunity.Create("Old Won", customer.Id, 80000m, OpportunityStage.Won, 100,
            today.AddDays(-45), users.AdminId, closeNote: "Won long ago", closedDate: today.AddDays(-45));

        ctx.Opportunities.AddRange(recentWon, oldWon);
        await ctx.SaveChangesAsync();

        using var harness = ServiceHarness.Create(Db, users.AdminId, Roles.Admin, clock);
        var service = harness.Services.GetRequiredService<IOpportunityService>();

        var board = await service.GetBoardAsync(new OpportunityFilter());
        var wonCol = board.First(c => c.Stage == OpportunityStage.Won);

        wonCol.Count.ShouldBe(1);
        wonCol.Amount.ShouldBe(40000m);
        wonCol.Cards.Count.ShouldBe(1);
        wonCol.Cards[0].Name.ShouldBe("Recent Won");
    }

    [Fact]
    public async Task Scope_respected_in_board_query()
    {
        await using var ctx = Db.CreateContext();
        var users = await TestUsers.CreateStandardAsync(ctx);

        var custNorth = Customer.Create("North Cust", null, "northc@example.com", "9876543210", null, null, null, CustomerStatus.Active, users.ExecN1Id, null);
        var custSouth = Customer.Create("South Cust", null, "southc@example.com", "9876543211", null, null, null, CustomerStatus.Active, users.ExecS1Id, null);
        ctx.Customers.AddRange(custNorth, custSouth);
        await ctx.SaveChangesAsync();

        var oppNorth = Opportunity.Create("North Deal", custNorth.Id, 10000m, OpportunityStage.Qualification, 20, new DateOnly(2026, 12, 1), users.ExecN1Id);
        var oppSouth = Opportunity.Create("South Deal", custSouth.Id, 20000m, OpportunityStage.Qualification, 20, new DateOnly(2026, 12, 1), users.ExecS1Id);
        ctx.Opportunities.AddRange(oppNorth, oppSouth);
        await ctx.SaveChangesAsync();

        // ExecN1 queries board
        using var harness = ServiceHarness.Create(Db, users.ExecN1Id, Roles.SalesExecutive);
        var service = harness.Services.GetRequiredService<IOpportunityService>();

        var board = await service.GetBoardAsync(new OpportunityFilter());
        var qualCol = board.First(c => c.Stage == OpportunityStage.Qualification);

        qualCol.Count.ShouldBe(1);
        qualCol.Amount.ShouldBe(10000m);
        qualCol.Cards.Count.ShouldBe(1);
        qualCol.Cards[0].Name.ShouldBe("North Deal");
    }
}

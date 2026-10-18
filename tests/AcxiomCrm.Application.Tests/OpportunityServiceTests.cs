using AcxiomCrm.Application.Common;
using AcxiomCrm.Application.Opportunities;
using AcxiomCrm.Domain.Entities;
using AcxiomCrm.Domain.Enums;
using AcxiomCrm.Domain.Rules;
using AcxiomCrm.Domain.Security;
using AcxiomCrm.TestSupport;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace AcxiomCrm.Application.Tests;

public class OpportunityServiceTests : DbTestBase
{
    public OpportunityServiceTests(SqlServerContainerFixture fixture) : base(fixture)
    {
    }

    [Fact]
    public async Task Create_valid_derives_status_weighted_and_owner()
    {
        await using var ctx = Db.CreateContext();
        var users = await TestUsers.CreateStandardAsync(ctx);

        var customer = Customer.Create("Opp Cust", "Cust Corp", "cust@example.com", "9876543210", null, null, null, CustomerStatus.Active, users.ExecN1Id, null);
        ctx.Customers.Add(customer);
        await ctx.SaveChangesAsync();

        using var harness = ServiceHarness.Create(Db, users.NorthManagerId, Roles.Manager);
        var service = harness.Services.GetRequiredService<IOpportunityService>();

        var cmd = new CreateOpportunityCommand(
            Name: "Big Deal",
            CustomerId: customer.Id,
            Amount: 1250000m,
            Probability: 40,
            ExpectedCloseDate: new DateOnly(2026, 12, 1),
            Stage: OpportunityStage.Proposal,
            Source: LeadSource.Website,
            OwnerId: users.ExecN1Id,
            Notes: "High value deal");

        var result = await service.CreateAsync(cmd);
        result.Succeeded.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value.Name.ShouldBe("Big Deal");
        result.Value.CustomerId.ShouldBe(customer.Id);
        result.Value.CustomerName.ShouldBe("Opp Cust");
        result.Value.Amount.ShouldBe(1250000m);
        result.Value.Probability.ShouldBe(40);
        result.Value.WeightedAmount.ShouldBe(500000.00m);
        result.Value.Stage.ShouldBe(OpportunityStage.Proposal);
        result.Value.Status.ShouldBe(OpportunityStatus.Open);
        result.Value.OwnerId.ShouldBe(users.ExecN1Id);
    }

    [Fact]
    public async Task Create_executive_owner_forced()
    {
        await using var ctx = Db.CreateContext();
        var users = await TestUsers.CreateStandardAsync(ctx);

        var customer = Customer.Create("Exec Cust", null, "exec.cust@example.com", "9876543210", null, null, null, CustomerStatus.Active, users.ExecN1Id, null);
        ctx.Customers.Add(customer);
        await ctx.SaveChangesAsync();

        using var harness = ServiceHarness.Create(Db, users.ExecN1Id, Roles.SalesExecutive);
        var service = harness.Services.GetRequiredService<IOpportunityService>();

        var cmd = new CreateOpportunityCommand(
            Name: "Exec Opp",
            CustomerId: customer.Id,
            Amount: 50000m,
            Probability: 20,
            ExpectedCloseDate: new DateOnly(2026, 11, 20),
            Stage: OpportunityStage.Qualification,
            Source: LeadSource.Referral,
            OwnerId: users.AdminId, // Tried to assign to admin
            Notes: null);

        var result = await service.CreateAsync(cmd);
        result.Succeeded.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value.OwnerId.ShouldBe(users.ExecN1Id); // Forced to caller
    }

    [Fact]
    public async Task Create_for_inactive_customer_rejected()
    {
        await using var ctx = Db.CreateContext();
        var users = await TestUsers.CreateStandardAsync(ctx);

        var customer = Customer.Create("Inactive Cust", null, "inact@example.com", "9876543210", null, null, null, CustomerStatus.Inactive, users.AdminId, null);
        ctx.Customers.Add(customer);
        await ctx.SaveChangesAsync();

        using var harness = ServiceHarness.Create(Db, users.AdminId, Roles.Admin);
        var service = harness.Services.GetRequiredService<IOpportunityService>();

        var cmd = new CreateOpportunityCommand(
            "Opp Inactive", customer.Id, 10000m, 20, new DateOnly(2026, 12, 1),
            OpportunityStage.Qualification, null, null, null);

        var result = await service.CreateAsync(cmd);
        result.Succeeded.ShouldBeFalse();
        result.Kind.ShouldBe(ErrorKind.Validation);
        result.Errors.ShouldContain(e => e.Field == "CustomerId" && e.Message == "Choose an active customer.");
    }

    [Fact]
    public async Task Create_for_out_of_scope_customer_rejected_same_message()
    {
        await using var ctx = Db.CreateContext();
        var users = await TestUsers.CreateStandardAsync(ctx);

        // South team customer
        var customer = Customer.Create("South Cust", null, "south@example.com", "9876543210", null, null, null, CustomerStatus.Active, users.ExecS1Id, null);
        ctx.Customers.Add(customer);
        await ctx.SaveChangesAsync();

        // North executive tries to create opportunity for South customer
        using var harness = ServiceHarness.Create(Db, users.ExecN1Id, Roles.SalesExecutive);
        var service = harness.Services.GetRequiredService<IOpportunityService>();

        var cmd = new CreateOpportunityCommand(
            "Cross Team Opp", customer.Id, 10000m, 20, new DateOnly(2026, 12, 1),
            OpportunityStage.Qualification, null, null, null);

        var result = await service.CreateAsync(cmd);
        result.Succeeded.ShouldBeFalse();
        result.Kind.ShouldBe(ErrorKind.Validation);
        result.Errors.ShouldContain(e => e.Field == "CustomerId" && e.Message == "Choose an active customer.");
    }

    [Fact]
    public async Task Create_with_closed_stage_rejected()
    {
        await using var ctx = Db.CreateContext();
        var users = await TestUsers.CreateStandardAsync(ctx);

        var customer = Customer.Create("Cust Valid", null, "valid@example.com", "9876543210", null, null, null, CustomerStatus.Active, users.AdminId, null);
        ctx.Customers.Add(customer);
        await ctx.SaveChangesAsync();

        using var harness = ServiceHarness.Create(Db, users.AdminId, Roles.Admin);
        var service = harness.Services.GetRequiredService<IOpportunityService>();

        var cmd = new CreateOpportunityCommand(
            "Direct Won", customer.Id, 10000m, 100, new DateOnly(2026, 12, 1),
            OpportunityStage.Won, null, null, null);

        var result = await service.CreateAsync(cmd);
        result.Succeeded.ShouldBeFalse();
        result.Kind.ShouldBe(ErrorKind.Validation);
        result.Errors.ShouldContain(e => e.Field == "Stage");
    }

    [Fact]
    public async Task Update_open_applies_rules()
    {
        await using var ctx = Db.CreateContext();
        var users = await TestUsers.CreateStandardAsync(ctx);

        var customer = Customer.Create("Cust Upd", null, "upd@example.com", "9876543210", null, null, null, CustomerStatus.Active, users.AdminId, null);
        ctx.Customers.Add(customer);
        await ctx.SaveChangesAsync();

        using var harness = ServiceHarness.Create(Db, users.AdminId, Roles.Admin);
        var service = harness.Services.GetRequiredService<IOpportunityService>();

        var createResult = await service.CreateAsync(new CreateOpportunityCommand(
            "Original Deal", customer.Id, 50000m, 20, new DateOnly(2026, 11, 15),
            OpportunityStage.Qualification, LeadSource.Website, null, "Initial notes"));
        createResult.Succeeded.ShouldBeTrue();

        var updateCmd = new UpdateOpportunityCommand(
            Name: "Updated Deal",
            CustomerId: customer.Id,
            Amount: 80000m,
            Probability: 50,
            ExpectedCloseDate: new DateOnly(2026, 11, 30),
            Source: LeadSource.Referral,
            OwnerId: users.AdminId,
            Notes: "Updated notes",
            RowVersion: createResult.Value!.RowVersion);

        var updateResult = await service.UpdateAsync(createResult.Value.Id, updateCmd);
        updateResult.Succeeded.ShouldBeTrue();
        updateResult.Value!.Name.ShouldBe("Updated Deal");
        updateResult.Value.Amount.ShouldBe(80000m);
        updateResult.Value.Probability.ShouldBe(50);
        updateResult.Value.WeightedAmount.ShouldBe(40000.00m);
        updateResult.Value.Notes.ShouldBe("Updated notes");
        updateResult.Value.Source.ShouldBe(LeadSource.Referral);
    }

    [Fact]
    public async Task Update_closed_is_conflict()
    {
        await using var ctx = Db.CreateContext();
        var users = await TestUsers.CreateStandardAsync(ctx);

        var customer = Customer.Create("Cust Closed", null, "closed@example.com", "9876543210", null, null, null, CustomerStatus.Active, users.AdminId, null);
        ctx.Customers.Add(customer);
        await ctx.SaveChangesAsync();

        var opp = Opportunity.Create(
            "Won Opp", customer.Id, 50000m, OpportunityStage.Won, 100,
            new DateOnly(2026, 10, 1), users.AdminId, closeNote: "Deal closed successfully", closedDate: new DateOnly(2026, 10, 1));
        ctx.Opportunities.Add(opp);
        await ctx.SaveChangesAsync();

        using var harness = ServiceHarness.Create(Db, users.AdminId, Roles.Admin);
        var service = harness.Services.GetRequiredService<IOpportunityService>();

        var updateCmd = new UpdateOpportunityCommand(
            "Try Update Won", customer.Id, 60000m, 100, new DateOnly(2026, 11, 1),
            null, users.AdminId, null, opp.RowVersion);

        var result = await service.UpdateAsync(opp.Id, updateCmd);
        result.Succeeded.ShouldBeFalse();
        result.Kind.ShouldBe(ErrorKind.Conflict);
        result.Errors[0].Message.ShouldContain("cannot be edited");
    }

    [Fact]
    public async Task Delete_soft_deletes()
    {
        await using var ctx = Db.CreateContext();
        var users = await TestUsers.CreateStandardAsync(ctx);

        var customer = Customer.Create("Cust Del", null, "del@example.com", "9876543210", null, null, null, CustomerStatus.Active, users.AdminId, null);
        ctx.Customers.Add(customer);
        await ctx.SaveChangesAsync();

        using var harness = ServiceHarness.Create(Db, users.AdminId, Roles.Admin);
        var service = harness.Services.GetRequiredService<IOpportunityService>();

        var created = await service.CreateAsync(new CreateOpportunityCommand(
            "To Delete", customer.Id, 25000m, 20, new DateOnly(2026, 12, 1),
            OpportunityStage.Qualification, null, null, null));
        created.Succeeded.ShouldBeTrue();

        var deleteResult = await service.DeleteAsync(created.Value!.Id);
        deleteResult.Succeeded.ShouldBeTrue();

        var getResult = await service.GetAsync(created.Value.Id);
        getResult.Succeeded.ShouldBeFalse();
        getResult.Kind.ShouldBe(ErrorKind.NotFound);

        await using var verifyCtx = Db.CreateContext();
        var rawOpp = await verifyCtx.Opportunities.IgnoreQueryFilters().FirstOrDefaultAsync(o => o.Id == created.Value.Id);
        rawOpp.ShouldNotBeNull();
        rawOpp.IsDeleted.ShouldBeTrue();
        rawOpp.DeletedBy.ShouldBe(users.AdminId);
        rawOpp.DeletedDate.ShouldNotBeNull();
    }

    [Fact]
    public async Task Search_filters_and_sorts()
    {
        await using var ctx = Db.CreateContext();
        var users = await TestUsers.CreateStandardAsync(ctx);

        var customer1 = Customer.Create("Alpha Customer", null, "alpha@example.com", "9876543210", null, null, null, CustomerStatus.Active, users.AdminId, null);
        var customer2 = Customer.Create("Beta Customer", null, "beta@example.com", "9876543211", null, null, null, CustomerStatus.Active, users.AdminId, null);
        ctx.Customers.AddRange(customer1, customer2);
        await ctx.SaveChangesAsync();

        using var harness = ServiceHarness.Create(Db, users.AdminId, Roles.Admin);
        var service = harness.Services.GetRequiredService<IOpportunityService>();

        await service.CreateAsync(new CreateOpportunityCommand(
            "Alpha Deal One", customer1.Id, 10000m, 20, new DateOnly(2026, 11, 10),
            OpportunityStage.Qualification, null, users.AdminId, null));

        await service.CreateAsync(new CreateOpportunityCommand(
            "Beta Deal Two", customer2.Id, 50000m, 50, new DateOnly(2026, 11, 20),
            OpportunityStage.Proposal, null, users.AdminId, null));

        // Filter by CustomerId
        var byCust = await service.SearchAsync(new OpportunityFilter(CustomerId: customer1.Id), new PageQuery());
        byCust.Total.ShouldBe(1);
        byCust.Items[0].Name.ShouldBe("Alpha Deal One");

        // Filter by Stage
        var byStage = await service.SearchAsync(new OpportunityFilter(Stage: OpportunityStage.Proposal), new PageQuery());
        byStage.Total.ShouldBe(1);
        byStage.Items[0].Name.ShouldBe("Beta Deal Two");

        // Sort by amount desc
        var sortAmtDesc = await service.SearchAsync(new OpportunityFilter(), new PageQuery(Sort: "amount", Desc: true));
        sortAmtDesc.Items[0].Amount.ShouldBe(50000m);
        sortAmtDesc.Items[1].Amount.ShouldBe(10000m);
    }

    [Fact]
    public async Task Weighted_in_list_equals_domain_rule()
    {
        await using var ctx = Db.CreateContext();
        var users = await TestUsers.CreateStandardAsync(ctx);

        var customer = Customer.Create("Weighted Cust", null, "weighted@example.com", "9876543210", null, null, null, CustomerStatus.Active, users.AdminId, null);
        ctx.Customers.Add(customer);
        await ctx.SaveChangesAsync();

        using var harness = ServiceHarness.Create(Db, users.AdminId, Roles.Admin);
        var service = harness.Services.GetRequiredService<IOpportunityService>();

        var amount = 33.33m;
        var probability = 33;
        var expectedWeighted = OpportunityRules.Weighted(amount, probability);

        var created = await service.CreateAsync(new CreateOpportunityCommand(
            "Round Test", customer.Id, amount, probability, new DateOnly(2026, 12, 1),
            OpportunityStage.Proposal, null, users.AdminId, null));
        created.Succeeded.ShouldBeTrue();
        created.Value!.WeightedAmount.ShouldBe(expectedWeighted);

        var list = await service.SearchAsync(new OpportunityFilter(Search: "Round Test"), new PageQuery());
        list.Items.Count.ShouldBe(1);
        list.Items[0].WeightedAmount.ShouldBe(expectedWeighted);
    }
}

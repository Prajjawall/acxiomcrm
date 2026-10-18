using AcxiomCrm.Application.Common;
using AcxiomCrm.Application.Opportunities;
using AcxiomCrm.Domain.Entities;
using AcxiomCrm.Domain.Enums;
using AcxiomCrm.Domain.Security;
using AcxiomCrm.Domain.Validation;
using AcxiomCrm.TestSupport;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace AcxiomCrm.Application.Tests;

public class OpportunityValidationTests : DbTestBase
{
    public OpportunityValidationTests(SqlServerContainerFixture fixture) : base(fixture)
    {
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-0.01)]
    public async Task Amount_zero_or_negative_rejected_with_canonical_message(decimal amount)
    {
        await using var ctx = Db.CreateContext();
        var users = await TestUsers.CreateStandardAsync(ctx);

        var customer = Customer.Create("Val Cust", null, "val@example.com", "9876543210", null, null, null, CustomerStatus.Active, users.AdminId, null);
        ctx.Customers.Add(customer);
        await ctx.SaveChangesAsync();

        using var harness = ServiceHarness.Create(Db, users.AdminId, Roles.Admin);
        var service = harness.Services.GetRequiredService<IOpportunityService>();

        var cmd = new CreateOpportunityCommand(
            "Test Deal", customer.Id, amount, 20, new DateOnly(2026, 12, 1),
            OpportunityStage.Qualification, null, null, null);

        var result = await service.CreateAsync(cmd);
        result.Succeeded.ShouldBeFalse();
        result.Kind.ShouldBe(ErrorKind.Validation);
        result.Errors.ShouldContain(e => e.Field == "Amount" && e.Message == Messages.AmountPositive);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(101)]
    [InlineData(255)]
    public async Task Probability_outside_range_rejected_with_canonical_message(int probability)
    {
        await using var ctx = Db.CreateContext();
        var users = await TestUsers.CreateStandardAsync(ctx);

        var customer = Customer.Create("Val Cust 2", null, "val2@example.com", "9876543210", null, null, null, CustomerStatus.Active, users.AdminId, null);
        ctx.Customers.Add(customer);
        await ctx.SaveChangesAsync();

        using var harness = ServiceHarness.Create(Db, users.AdminId, Roles.Admin);
        var service = harness.Services.GetRequiredService<IOpportunityService>();

        var cmd = new CreateOpportunityCommand(
            "Test Deal", customer.Id, 1000m, probability, new DateOnly(2026, 12, 1),
            OpportunityStage.Qualification, null, null, null);

        var result = await service.CreateAsync(cmd);
        result.Succeeded.ShouldBeFalse();
        result.Kind.ShouldBe(ErrorKind.Validation);
        result.Errors.ShouldContain(e => e.Field == "Probability" && e.Message == Messages.ProbabilityRange);
    }

    [Fact]
    public async Task Expected_close_date_in_past_rejected_with_canonical_message()
    {
        await using var ctx = Db.CreateContext();
        var users = await TestUsers.CreateStandardAsync(ctx);

        var customer = Customer.Create("Val Cust 3", null, "val3@example.com", "9876543210", null, null, null, CustomerStatus.Active, users.AdminId, null);
        ctx.Customers.Add(customer);
        await ctx.SaveChangesAsync();

        // Fixed clock: today is 2026-10-16
        var clock = new FakeClock(new DateTime(2026, 10, 16, 12, 0, 0, DateTimeKind.Utc));
        using var harness = ServiceHarness.Create(Db, users.AdminId, Roles.Admin, clock);
        var service = harness.Services.GetRequiredService<IOpportunityService>();

        var cmd = new CreateOpportunityCommand(
            "Test Deal", customer.Id, 1000m, 20, new DateOnly(2026, 10, 15), // Yesterday
            OpportunityStage.Qualification, null, null, null);

        var result = await service.CreateAsync(cmd);
        result.Succeeded.ShouldBeFalse();
        result.Kind.ShouldBe(ErrorKind.Validation);
        result.Errors.ShouldContain(e => e.Field == "ExpectedCloseDate" && e.Message == Messages.ExpectedCloseDatePast);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public async Task Blank_name_rejected_with_canonical_message(string? name)
    {
        await using var ctx = Db.CreateContext();
        var users = await TestUsers.CreateStandardAsync(ctx);

        var customer = Customer.Create("Val Cust 4", null, "val4@example.com", "9876543210", null, null, null, CustomerStatus.Active, users.AdminId, null);
        ctx.Customers.Add(customer);
        await ctx.SaveChangesAsync();

        using var harness = ServiceHarness.Create(Db, users.AdminId, Roles.Admin);
        var service = harness.Services.GetRequiredService<IOpportunityService>();

        var cmd = new CreateOpportunityCommand(
            name!, customer.Id, 1000m, 20, new DateOnly(2026, 12, 1),
            OpportunityStage.Qualification, null, null, null);

        var result = await service.CreateAsync(cmd);
        result.Succeeded.ShouldBeFalse();
        result.Kind.ShouldBe(ErrorKind.Validation);
        result.Errors.ShouldContain(e => e.Field == "Name" && e.Message == Messages.Required("Opportunity name"));
    }

    [Fact]
    public async Task Name_over_max_length_rejected_with_canonical_message()
    {
        await using var ctx = Db.CreateContext();
        var users = await TestUsers.CreateStandardAsync(ctx);

        var customer = Customer.Create("Val Cust 5", null, "val5@example.com", "9876543210", null, null, null, CustomerStatus.Active, users.AdminId, null);
        ctx.Customers.Add(customer);
        await ctx.SaveChangesAsync();

        using var harness = ServiceHarness.Create(Db, users.AdminId, Roles.Admin);
        var service = harness.Services.GetRequiredService<IOpportunityService>();

        var longName = new string('A', Limits.Name + 1);
        var cmd = new CreateOpportunityCommand(
            longName, customer.Id, 1000m, 20, new DateOnly(2026, 12, 1),
            OpportunityStage.Qualification, null, null, null);

        var result = await service.CreateAsync(cmd);
        result.Succeeded.ShouldBeFalse();
        result.Kind.ShouldBe(ErrorKind.Validation);
        result.Errors.ShouldContain(e => e.Field == "Name" && e.Message == Messages.MaxLength("Opportunity name", Limits.Name));
    }

    [Fact]
    public async Task Midnight_boundary_checks_business_today_accurately()
    {
        await using var ctx = Db.CreateContext();
        var users = await TestUsers.CreateStandardAsync(ctx);

        var customer = Customer.Create("Val Cust Midnight", null, "valmid@example.com", "9876543210", null, null, null, CustomerStatus.Active, users.AdminId, null);
        ctx.Customers.Add(customer);
        await ctx.SaveChangesAsync();

        var testDate = new DateOnly(2026, 10, 16);

        // 1. UTC 18:29 on 2026-10-16 -> In Asia/Kolkata (+5:30) it is 23:59 on 2026-10-16. testDate is today -> valid!
        var clockBeforeMidnight = new FakeClock(new DateTime(2026, 10, 16, 18, 29, 0, DateTimeKind.Utc));
        using (var harness1 = ServiceHarness.Create(Db, users.AdminId, Roles.Admin, clockBeforeMidnight))
        {
            var service1 = harness1.Services.GetRequiredService<IOpportunityService>();
            var cmd1 = new CreateOpportunityCommand("Midnight Deal 1", customer.Id, 5000m, 20, testDate, OpportunityStage.Qualification, null, null, null);
            var res1 = await service1.CreateAsync(cmd1);
            res1.Succeeded.ShouldBeTrue();
        }

        // 2. UTC 18:30 on 2026-10-16 -> In Asia/Kolkata (+5:30) it is 00:00 on 2026-10-17. testDate is now yesterday -> rejected!
        var clockAfterMidnight = new FakeClock(new DateTime(2026, 10, 16, 18, 30, 0, DateTimeKind.Utc));
        using (var harness2 = ServiceHarness.Create(Db, users.AdminId, Roles.Admin, clockAfterMidnight))
        {
            var service2 = harness2.Services.GetRequiredService<IOpportunityService>();
            var cmd2 = new CreateOpportunityCommand("Midnight Deal 2", customer.Id, 5000m, 20, testDate, OpportunityStage.Qualification, null, null, null);
            var res2 = await service2.CreateAsync(cmd2);
            res2.Succeeded.ShouldBeFalse();
            res2.Kind.ShouldBe(ErrorKind.Validation);
            res2.Errors.ShouldContain(e => e.Field == "ExpectedCloseDate" && e.Message == Messages.ExpectedCloseDatePast);
        }
    }
}

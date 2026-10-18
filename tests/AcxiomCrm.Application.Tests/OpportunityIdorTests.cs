using AcxiomCrm.Application.Common;
using AcxiomCrm.Application.Opportunities;
using AcxiomCrm.Domain.Entities;
using AcxiomCrm.Domain.Enums;
using AcxiomCrm.Domain.Security;
using AcxiomCrm.TestSupport;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace AcxiomCrm.Application.Tests;

public class OpportunityIdorTests : DbTestBase
{
    public OpportunityIdorTests(SqlServerContainerFixture fixture) : base(fixture)
    {
    }

    [Fact]
    public async Task Executive_and_other_manager_cannot_access_out_of_scope_opportunity()
    {
        await using var ctx = Db.CreateContext();
        var users = await TestUsers.CreateStandardAsync(ctx);

        var customer = Customer.Create("North Cust", null, "northcust@example.com", "9876543210", null, null, null, CustomerStatus.Active, users.ExecN1Id, null);
        ctx.Customers.Add(customer);
        await ctx.SaveChangesAsync();

        // North Exec1 creates an opportunity
        int northOppId;
        byte[] rowVersion;
        using (var n1Harness = ServiceHarness.Create(Db, users.ExecN1Id, Roles.SalesExecutive))
        {
            var service = n1Harness.Services.GetRequiredService<IOpportunityService>();
            var res = await service.CreateAsync(new CreateOpportunityCommand(
                "North Opp", customer.Id, 50000m, 20, new DateOnly(2026, 12, 1),
                OpportunityStage.Qualification, null, null, null));
            res.Succeeded.ShouldBeTrue();
            northOppId = res.Value!.Id;
            rowVersion = res.Value.RowVersion;
        }

        // ExecSouth1 (different executive, different team) attempts access
        using (var s1Harness = ServiceHarness.Create(Db, users.ExecS1Id, Roles.SalesExecutive))
        {
            var service = s1Harness.Services.GetRequiredService<IOpportunityService>();

            // Get -> NotFound
            var get = await service.GetAsync(northOppId);
            get.Succeeded.ShouldBeFalse();
            get.Kind.ShouldBe(ErrorKind.NotFound);

            // Update -> NotFound
            var update = await service.UpdateAsync(northOppId, new UpdateOpportunityCommand(
                "Hacked", customer.Id, 1000m, 20, new DateOnly(2026, 12, 1), null, null, null, rowVersion));
            update.Succeeded.ShouldBeFalse();
            update.Kind.ShouldBe(ErrorKind.NotFound);

            // Delete -> NotFound
            var delete = await service.DeleteAsync(northOppId);
            delete.Succeeded.ShouldBeFalse();
            delete.Kind.ShouldBe(ErrorKind.NotFound);

            // ChangeStage -> NotFound
            var stage = await service.ChangeStageAsync(northOppId, OpportunityStage.Proposal, null, rowVersion);
            stage.Succeeded.ShouldBeFalse();
            stage.Kind.ShouldBe(ErrorKind.NotFound);

            // Search -> excludes
            var search = await service.SearchAsync(new OpportunityFilter(), new PageQuery());
            search.Items.ShouldNotContain(o => o.Id == northOppId);

            // Board -> excludes
            var board = await service.GetBoardAsync(new OpportunityFilter());
            board.SelectMany(c => c.Cards).ShouldNotContain(o => o.Id == northOppId);
        }

        // South Manager (other team manager) attempts access
        using (var smHarness = ServiceHarness.Create(Db, users.SouthManagerId, Roles.Manager))
        {
            var service = smHarness.Services.GetRequiredService<IOpportunityService>();

            var get = await service.GetAsync(northOppId);
            get.Succeeded.ShouldBeFalse();
            get.Kind.ShouldBe(ErrorKind.NotFound);

            var delete = await service.DeleteAsync(northOppId);
            delete.Succeeded.ShouldBeFalse();
            delete.Kind.ShouldBe(ErrorKind.NotFound);

            var reopen = await service.ReopenAsync(northOppId, rowVersion);
            reopen.Succeeded.ShouldBeFalse();
            reopen.Kind.ShouldBe(ErrorKind.NotFound);
        }
    }
}

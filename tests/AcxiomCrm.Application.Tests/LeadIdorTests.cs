using AcxiomCrm.Application.Common;
using AcxiomCrm.Application.Leads;
using AcxiomCrm.Domain.Enums;
using AcxiomCrm.Domain.Security;
using AcxiomCrm.TestSupport;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace AcxiomCrm.Application.Tests;

public class LeadIdorTests : DbTestBase
{
    public LeadIdorTests(SqlServerContainerFixture fixture) : base(fixture)
    {
    }

    [Fact]
    public async Task Executive_and_other_manager_cannot_access_out_of_scope_lead()
    {
        await using var ctx = Db.CreateContext();
        var users = await TestUsers.CreateStandardAsync(ctx);

        // ExecNorth1 creates a lead
        int northLeadId;
        byte[] rowVersion;
        using (var n1Harness = ServiceHarness.Create(Db, users.ExecN1Id, Roles.SalesExecutive))
        {
            var service = n1Harness.Services.GetRequiredService<ILeadService>();
            var res = await service.CreateAsync(new CreateLeadCommand(
                "North Lead", null, "north.lead@example.com", "9876543210",
                LeadSource.Website, LeadPriority.Medium, 1000m, null, null));
            res.Succeeded.ShouldBeTrue();
            northLeadId = res.Value!.Id;
            rowVersion = res.Value.RowVersion;
        }

        // ExecSouth1 (different executive, different team) attempts access
        using (var s1Harness = ServiceHarness.Create(Db, users.ExecS1Id, Roles.SalesExecutive))
        {
            var service = s1Harness.Services.GetRequiredService<ILeadService>();

            // Get -> NotFound
            var get = await service.GetAsync(northLeadId);
            get.Succeeded.ShouldBeFalse();
            get.Kind.ShouldBe(ErrorKind.NotFound);

            // Update -> NotFound
            var update = await service.UpdateAsync(northLeadId, new UpdateLeadCommand(
                "Hacked", null, "hacked@example.com", "9876543210",
                LeadSource.Website, LeadPriority.Low, 500m, null, rowVersion));
            update.Succeeded.ShouldBeFalse();
            update.Kind.ShouldBe(ErrorKind.NotFound);

            // Delete -> NotFound
            var delete = await service.DeleteAsync(northLeadId);
            delete.Succeeded.ShouldBeFalse();
            delete.Kind.ShouldBe(ErrorKind.NotFound);

            // ChangeStatus -> NotFound
            var status = await service.ChangeStatusAsync(northLeadId, LeadStatus.Contacted, rowVersion);
            status.Succeeded.ShouldBeFalse();
            status.Kind.ShouldBe(ErrorKind.NotFound);

            // Search -> excludes
            var search = await service.SearchAsync(new LeadFilter(), new PageQuery());
            search.Items.ShouldNotContain(l => l.Id == northLeadId);
        }

        // South Manager (other team manager) attempts access
        using (var smHarness = ServiceHarness.Create(Db, users.SouthManagerId, Roles.Manager))
        {
            var service = smHarness.Services.GetRequiredService<ILeadService>();

            var get = await service.GetAsync(northLeadId);
            get.Succeeded.ShouldBeFalse();
            get.Kind.ShouldBe(ErrorKind.NotFound);

            var assign = await service.AssignAsync(northLeadId, users.ExecS1Id, rowVersion);
            assign.Succeeded.ShouldBeFalse();
            assign.Kind.ShouldBe(ErrorKind.NotFound);
        }
    }
}

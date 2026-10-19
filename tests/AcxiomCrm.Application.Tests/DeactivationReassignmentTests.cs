using AcxiomCrm.Application.Common;
using AcxiomCrm.Application.Users;
using AcxiomCrm.Domain.Entities;
using AcxiomCrm.Domain.Enums;
using AcxiomCrm.Domain.Security;
using AcxiomCrm.Infrastructure.Identity;
using AcxiomCrm.TestSupport;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace AcxiomCrm.Application.Tests;

public class DeactivationReassignmentTests : DbTestBase
{
    private static readonly DateTime FixedDate = new(2026, 10, 19, 10, 0, 0, DateTimeKind.Utc);

    public DeactivationReassignmentTests(SqlServerContainerFixture fixture) : base(fixture)
    {
    }

    [Fact]
    public async Task Deactivation_with_reassignment_moves_all_open_records()
    {
        await using var ctx = Db.CreateContext();
        var users = await TestUsers.CreateStandardAsync(ctx);

        var cust = Customer.Create("C1", null, "c1-reassign@example.com", "9876543210", null, null, null, CustomerStatus.Active, users.ExecN1Id, null);
        ctx.Customers.Add(cust);
        await ctx.SaveChangesAsync();

        var lead = Lead.Create("L1", null, "l1-reassign@example.com", "9876543211", LeadSource.Website, LeadPriority.High, 1000m, users.ExecN1Id, null);
        ctx.Leads.Add(lead);

        var opp = Opportunity.Create("O1", cust.Id, 5000m, OpportunityStage.Proposal, 50, DateOnly.FromDateTime(FixedDate).AddDays(10), users.ExecN1Id);
        ctx.Opportunities.Add(opp);

        var fu = FollowUp.Create(cust.Id, null, null, FixedDate.AddDays(1), "Follow up", FollowUpType.Call, null, users.ExecN1Id, false);
        ctx.FollowUps.Add(fu);

        var act = Activity.Create(ActivityType.Call, "Call made", null, FixedDate, cust.Id, null, null, users.ExecN1Id);
        ctx.Activities.Add(act);

        await ctx.SaveChangesAsync();

        var clock = new FakeClock(FixedDate);
        using var harness = ServiceHarness.Create(Db, users.AdminId, Roles.Admin, clock);
        var service = harness.Services.GetRequiredService<IUserAdminService>();
        var userManager = harness.Services.GetRequiredService<UserManager<ApplicationUser>>();

        // 1. Reassigning to inactive user fails
        var failCmd = new DeactivateUserCommand(ReassignToUserId: users.ExecN2Id); // ExecN2 is inactive!
        var failRes = await service.DeactivateAsync(users.ExecN1Id, failCmd);
        failRes.Succeeded.ShouldBeFalse();
        failRes.Kind.ShouldBe(ErrorKind.Validation);
        failRes.Errors.ShouldContain(e => e.Message == "Reassignment target must be an active user.");

        // 2. Reassigning to active user (ExecS1Id) succeeds
        var successCmd = new DeactivateUserCommand(ReassignToUserId: users.ExecS1Id);
        var successRes = await service.DeactivateAsync(users.ExecN1Id, successCmd);
        successRes.Succeeded.ShouldBeTrue();

        // Check user deactivated
        var deactivatedUser = await userManager.FindByIdAsync(users.ExecN1Id);
        deactivatedUser.ShouldNotBeNull();
        deactivatedUser.IsActive.ShouldBeFalse();

        // Check records reassigned
        var refreshedLead = await ctx.Leads.AsNoTracking().FirstAsync(l => l.Id == lead.Id);
        refreshedLead.OwnerId.ShouldBe(users.ExecS1Id);

        var refreshedOpp = await ctx.Opportunities.AsNoTracking().FirstAsync(o => o.Id == opp.Id);
        refreshedOpp.OwnerId.ShouldBe(users.ExecS1Id);

        var refreshedFu = await ctx.FollowUps.AsNoTracking().FirstAsync(f => f.Id == fu.Id);
        refreshedFu.OwnerId.ShouldBe(users.ExecS1Id);

        var refreshedAct = await ctx.Activities.AsNoTracking().FirstAsync(a => a.Id == act.Id);
        refreshedAct.OwnerId.ShouldBe(users.ExecS1Id);

        // Audit check
        var assignedAudit = await ctx.AuditLogs
            .Where(a => a.RecordId == users.ExecN1Id && a.Action == "Assigned")
            .FirstOrDefaultAsync();
        assignedAudit.ShouldNotBeNull();
        assignedAudit.Details?.Contains("reassigned=4").ShouldBeTrue();
    }
}

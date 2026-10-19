using AcxiomCrm.Application.Common;
using AcxiomCrm.Application.Users;
using AcxiomCrm.Domain.Entities;
using AcxiomCrm.Domain.Enums;
using AcxiomCrm.Domain.Security;
using AcxiomCrm.TestSupport;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace AcxiomCrm.Application.Tests;

public class UserAdminServiceTests : DbTestBase
{
    private static readonly DateTime FixedDate = new(2026, 10, 19, 10, 0, 0, DateTimeKind.Utc);

    public UserAdminServiceTests(SqlServerContainerFixture fixture) : base(fixture)
    {
    }

    [Fact]
    public async Task Create_valid_for_each_role()
    {
        await using var ctx = Db.CreateContext();
        var users = await TestUsers.CreateStandardAsync(ctx);

        var clock = new FakeClock(FixedDate);
        using var harness = ServiceHarness.Create(Db, users.AdminId, Roles.Admin, clock);
        var service = harness.Services.GetRequiredService<IUserAdminService>();

        // 1. Create Admin
        var adminCmd = new CreateUserCommand("New Admin", "new.admin", "new.admin@acxiom.com", Roles.Admin, null, true);
        var adminRes = await service.CreateAsync(adminCmd);
        adminRes.Succeeded.ShouldBeTrue();
        adminRes.Value!.UserId.ShouldNotBeNullOrWhiteSpace();
        adminRes.Value.TemporaryPassword.Length.ShouldBeGreaterThanOrEqualTo(16);

        // 2. Create Manager
        var mgrCmd = new CreateUserCommand("New Manager", "new.mgr", "new.mgr@acxiom.com", Roles.Manager, null, true);
        var mgrRes = await service.CreateAsync(mgrCmd);
        mgrRes.Succeeded.ShouldBeTrue();
        mgrRes.Value!.UserId.ShouldNotBeNullOrWhiteSpace();

        // 3. Create SalesExecutive with Manager
        var execCmd = new CreateUserCommand("New Exec", "new.exec", "new.exec@acxiom.com", Roles.SalesExecutive, mgrRes.Value.UserId, true);
        var execRes = await service.CreateAsync(execCmd);
        execRes.Succeeded.ShouldBeTrue();
        execRes.Value!.UserId.ShouldNotBeNullOrWhiteSpace();

        // Verify Get
        var execDetail = await service.GetAsync(execRes.Value.UserId);
        execDetail.Succeeded.ShouldBeTrue();
        execDetail.Value!.FullName.ShouldBe("New Exec");
        execDetail.Value.Role.ShouldBe(Roles.SalesExecutive);
        execDetail.Value.ManagerId.ShouldBe(mgrRes.Value.UserId);
        execDetail.Value.ManagerName.ShouldBe("New Manager");
        execDetail.Value.MustChangePassword.ShouldBeTrue();
    }

    [Fact]
    public async Task Create_duplicate_username_fails()
    {
        await using var ctx = Db.CreateContext();
        var users = await TestUsers.CreateStandardAsync(ctx);

        var clock = new FakeClock(FixedDate);
        using var harness = ServiceHarness.Create(Db, users.AdminId, Roles.Admin, clock);
        var service = harness.Services.GetRequiredService<IUserAdminService>();

        var cmd = new CreateUserCommand("Duplicate User", "admin", "different.email@acxiom.com", Roles.Admin, null, true);
        var res = await service.CreateAsync(cmd);
        res.Succeeded.ShouldBeFalse();
        res.Kind.ShouldBe(ErrorKind.Conflict);
        res.Errors.ShouldContain(e => e.Message == "Username is already taken.");
    }

    [Fact]
    public async Task Create_duplicate_email_fails()
    {
        await using var ctx = Db.CreateContext();
        var users = await TestUsers.CreateStandardAsync(ctx);

        var clock = new FakeClock(FixedDate);
        using var harness = ServiceHarness.Create(Db, users.AdminId, Roles.Admin, clock);
        var service = harness.Services.GetRequiredService<IUserAdminService>();

        var cmd = new CreateUserCommand("Duplicate User", "unique.admin", "admin@acxiom.com", Roles.Admin, null, true);
        var res = await service.CreateAsync(cmd);
        res.Succeeded.ShouldBeFalse();
        res.Kind.ShouldBe(ErrorKind.Conflict);
        res.Errors.ShouldContain(e => e.Message == "Email address is already in use.");
    }

    [Fact]
    public async Task Manager_role_requires_no_manager()
    {
        await using var ctx = Db.CreateContext();
        var users = await TestUsers.CreateStandardAsync(ctx);

        var clock = new FakeClock(FixedDate);
        using var harness = ServiceHarness.Create(Db, users.AdminId, Roles.Admin, clock);
        var service = harness.Services.GetRequiredService<IUserAdminService>();

        var cmd = new CreateUserCommand("Invalid Mgr", "invalid.mgr", "invalid.mgr@acxiom.com", Roles.Manager, users.NorthManagerId, true);
        var res = await service.CreateAsync(cmd);
        res.Succeeded.ShouldBeFalse();
        res.Kind.ShouldBe(ErrorKind.Validation);
        res.Errors.ShouldContain(e => e.Message == "Manager is only valid for Sales Executives.");
    }

    [Fact]
    public async Task SalesExecutive_manager_must_be_active_manager()
    {
        await using var ctx = Db.CreateContext();
        var users = await TestUsers.CreateStandardAsync(ctx);

        var clock = new FakeClock(FixedDate);
        using var harness = ServiceHarness.Create(Db, users.AdminId, Roles.Admin, clock);
        var service = harness.Services.GetRequiredService<IUserAdminService>();

        // Pointing to another SalesExecutive instead of a Manager
        var cmd1 = new CreateUserCommand("Invalid Exec", "invalid.exec1", "invalid.exec1@acxiom.com", Roles.SalesExecutive, users.ExecN1Id, true);
        var res1 = await service.CreateAsync(cmd1);
        res1.Succeeded.ShouldBeFalse();
        res1.Kind.ShouldBe(ErrorKind.Validation);
        res1.Errors.ShouldContain(e => e.Message == "Manager must be an active manager.");

        // Pointing to non-existent manager
        var cmd2 = new CreateUserCommand("Invalid Exec", "invalid.exec2", "invalid.exec2@acxiom.com", Roles.SalesExecutive, "non-existent-id", true);
        var res2 = await service.CreateAsync(cmd2);
        res2.Succeeded.ShouldBeFalse();
        res2.Kind.ShouldBe(ErrorKind.Validation);
        res2.Errors.ShouldContain(e => e.Message == "Manager must be an active manager.");
    }

    [Fact]
    public async Task Search_filters_and_paging_work()
    {
        await using var ctx = Db.CreateContext();
        var users = await TestUsers.CreateStandardAsync(ctx);

        var clock = new FakeClock(FixedDate);
        using var harness = ServiceHarness.Create(Db, users.AdminId, Roles.Admin, clock);
        var service = harness.Services.GetRequiredService<IUserAdminService>();

        // Search by name
        var searchRes = await service.SearchAsync(new UserFilter(Search: "North"), new PageQuery(1, 10));
        searchRes.Total.ShouldBeGreaterThanOrEqualTo(2);
        searchRes.Items.All(i => i.FullName.Contains("North", StringComparison.OrdinalIgnoreCase)).ShouldBeTrue();

        // Search by role
        var roleRes = await service.SearchAsync(new UserFilter(Role: Roles.Manager), new PageQuery(1, 10));
        roleRes.Items.All(i => i.Role == Roles.Manager).ShouldBeTrue();

        // Search by active
        var activeRes = await service.SearchAsync(new UserFilter(Active: false), new PageQuery(1, 10));
        activeRes.Items.Any(i => i.Id == users.ExecN2Id).ShouldBeTrue();
    }

    [Fact]
    public async Task Get_returns_counts_of_open_work()
    {
        await using var ctx = Db.CreateContext();
        var users = await TestUsers.CreateStandardAsync(ctx);

        var cust = Customer.Create("C1", null, "c1-open@example.com", "9876543210", null, null, null, CustomerStatus.Active, users.ExecN1Id, null);
        ctx.Customers.Add(cust);
        await ctx.SaveChangesAsync();

        var lead = Lead.Create("L1", null, "l1-open@example.com", "9876543211", LeadSource.Website, LeadPriority.High, 1000m, users.ExecN1Id, null);
        ctx.Leads.Add(lead);

        var opp = Opportunity.Create("O1", cust.Id, 5000m, OpportunityStage.Proposal, 50, DateOnly.FromDateTime(FixedDate).AddDays(10), users.ExecN1Id);
        ctx.Opportunities.Add(opp);

        var fu = FollowUp.Create(cust.Id, null, null, FixedDate.AddDays(1), "Follow up", FollowUpType.Call, null, users.ExecN1Id, false);
        ctx.FollowUps.Add(fu);

        await ctx.SaveChangesAsync();

        var clock = new FakeClock(FixedDate);
        using var harness = ServiceHarness.Create(Db, users.AdminId, Roles.Admin, clock);
        var service = harness.Services.GetRequiredService<IUserAdminService>();

        var res = await service.GetAsync(users.ExecN1Id);
        res.Succeeded.ShouldBeTrue();
        res.Value!.OpenLeads.ShouldBe(1);
        res.Value.OpenOpportunities.ShouldBe(1);
        res.Value.PlannedFollowUps.ShouldBe(1);
    }
}

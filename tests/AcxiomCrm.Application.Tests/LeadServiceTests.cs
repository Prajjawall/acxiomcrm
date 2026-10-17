using System.Reflection;
using AcxiomCrm.Application.Common;
using AcxiomCrm.Application.Leads;
using AcxiomCrm.Domain.Enums;
using AcxiomCrm.Domain.Security;
using AcxiomCrm.TestSupport;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace AcxiomCrm.Application.Tests;

public class LeadServiceTests : DbTestBase
{
    public LeadServiceTests(SqlServerContainerFixture fixture) : base(fixture)
    {
    }

    [Fact]
    public async Task Create_defaults_status_New_and_assigns_code()
    {
        await using var ctx = Db.CreateContext();
        var users = await TestUsers.CreateStandardAsync(ctx);

        using var harness = ServiceHarness.Create(Db, users.ExecN1Id, Roles.SalesExecutive);
        var service = harness.Services.GetRequiredService<ILeadService>();

        var cmd = new CreateLeadCommand(
            Name: "Alice Smith",
            CompanyName: "Acme Corp",
            Email: "alice@example.com",
            Phone: "9876543210",
            Source: LeadSource.Website,
            Priority: LeadPriority.High,
            ExpectedValue: 50000m,
            OwnerId: null,
            Notes: "Interested in enterprise plan");

        var result = await service.CreateAsync(cmd);
        result.Succeeded.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value.Status.ShouldBe(LeadStatus.New);
        result.Value.Code.ShouldStartWith("LEAD-");
        result.Value.Code.Length.ShouldBe(11);
        result.Value.OwnerId.ShouldBe(users.ExecN1Id);
    }

    [Fact]
    public void Create_with_non_New_status_rejected()
    {
        var statusProp = typeof(CreateLeadCommand).GetProperty("Status", BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
        statusProp.ShouldBeNull("CreateLeadCommand must not expose a Status property to prevent setting non-New status on creation.");
    }

    [Fact]
    public async Task Executive_creates_for_self_only()
    {
        await using var ctx = Db.CreateContext();
        var users = await TestUsers.CreateStandardAsync(ctx);

        using var harness = ServiceHarness.Create(Db, users.ExecN1Id, Roles.SalesExecutive);
        var service = harness.Services.GetRequiredService<ILeadService>();

        var cmd = new CreateLeadCommand(
            Name: "Bob Jones",
            CompanyName: null,
            Email: "bob@example.com",
            Phone: null,
            Source: LeadSource.ColdCall,
            Priority: LeadPriority.Low,
            ExpectedValue: 1000m,
            OwnerId: users.ExecS1Id, // Trying to assign to South Executive
            Notes: null);

        var result = await service.CreateAsync(cmd);
        result.Succeeded.ShouldBeTrue();
        result.Value!.OwnerId.ShouldBe(users.ExecN1Id); // Forced to caller
    }

    [Fact]
    public async Task Manager_creates_for_team_member()
    {
        await using var ctx = Db.CreateContext();
        var users = await TestUsers.CreateStandardAsync(ctx);

        using var harness = ServiceHarness.Create(Db, users.NorthManagerId, Roles.Manager);
        var service = harness.Services.GetRequiredService<ILeadService>();

        var cmd = new CreateLeadCommand(
            Name: "Charlie Brown",
            CompanyName: "Peanuts Inc",
            Email: "charlie@example.com",
            Phone: "9876543211",
            Source: LeadSource.Referral,
            Priority: LeadPriority.Medium,
            ExpectedValue: 25000m,
            OwnerId: users.ExecN1Id, // Member of North team
            Notes: null);

        var result = await service.CreateAsync(cmd);
        result.Succeeded.ShouldBeTrue();
        result.Value!.OwnerId.ShouldBe(users.ExecN1Id);
    }

    [Fact]
    public async Task Manager_creates_for_other_team_member_fails()
    {
        await using var ctx = Db.CreateContext();
        var users = await TestUsers.CreateStandardAsync(ctx);

        using var harness = ServiceHarness.Create(Db, users.NorthManagerId, Roles.Manager);
        var service = harness.Services.GetRequiredService<ILeadService>();

        var cmd = new CreateLeadCommand(
            Name: "David Lee",
            CompanyName: null,
            Email: "david@example.com",
            Phone: "9876543212",
            Source: LeadSource.Event,
            Priority: LeadPriority.Medium,
            ExpectedValue: 15000m,
            OwnerId: users.ExecS1Id, // South team member - out of scope
            Notes: null);

        var result = await service.CreateAsync(cmd);
        result.Succeeded.ShouldBeFalse();
        result.Kind.ShouldBe(ErrorKind.Validation);
        result.Errors.ShouldContain(e => e.Field == "OwnerId");
    }

    [Fact]
    public async Task Update_cannot_change_status_or_owner()
    {
        typeof(UpdateLeadCommand).GetProperty("Status", BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase).ShouldBeNull();
        typeof(UpdateLeadCommand).GetProperty("OwnerId", BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase).ShouldBeNull();

        await using var ctx = Db.CreateContext();
        var users = await TestUsers.CreateStandardAsync(ctx);

        using var harness = ServiceHarness.Create(Db, users.AdminId, Roles.Admin);
        var service = harness.Services.GetRequiredService<ILeadService>();

        var created = await service.CreateAsync(new CreateLeadCommand(
            "Original Name", "Original Co", "orig@example.com", "9876543210",
            LeadSource.Website, LeadPriority.Low, 1000m, users.ExecN1Id, null));
        created.Succeeded.ShouldBeTrue();

        var updateCmd = new UpdateLeadCommand(
            "New Name", "New Co", "new@example.com", "9876543211",
            LeadSource.SocialMedia, LeadPriority.High, 5000m, "Updated Notes", created.Value!.RowVersion);

        var updateResult = await service.UpdateAsync(created.Value.Id, updateCmd);
        updateResult.Succeeded.ShouldBeTrue();
        updateResult.Value!.Name.ShouldBe("New Name");
        updateResult.Value.Status.ShouldBe(LeadStatus.New);
        updateResult.Value.OwnerId.ShouldBe(users.ExecN1Id);
    }

    [Fact]
    public async Task Update_converted_or_lost_is_rejected_with_message()
    {
        await using var ctx = Db.CreateContext();
        var users = await TestUsers.CreateStandardAsync(ctx);

        using var harness = ServiceHarness.Create(Db, users.AdminId, Roles.Admin);
        var service = harness.Services.GetRequiredService<ILeadService>();

        var created = await service.CreateAsync(new CreateLeadCommand(
            "Lead To Lose", null, "lose@example.com", "9876543210",
            LeadSource.Website, LeadPriority.Low, 1000m, null, null));
        created.Succeeded.ShouldBeTrue();

        // Move to Lost
        var statusResult = await service.ChangeStatusAsync(created.Value!.Id, LeadStatus.Lost, created.Value.RowVersion);
        statusResult.Succeeded.ShouldBeTrue();

        // Attempt edit on Lost lead
        var updateCmd = new UpdateLeadCommand(
            "Attempted Edit", null, "lose@example.com", "9876543210",
            LeadSource.Website, LeadPriority.Low, 1000m, null, statusResult.Value!.RowVersion);

        var updateResult = await service.UpdateAsync(created.Value.Id, updateCmd);
        updateResult.Succeeded.ShouldBeFalse();
        updateResult.Kind.ShouldBe(ErrorKind.Conflict);
        updateResult.Errors[0].Message.ShouldBe("This lead is Lost and cannot be edited.");
    }

    [Fact]
    public async Task Delete_soft_deletes()
    {
        await using var ctx = Db.CreateContext();
        var users = await TestUsers.CreateStandardAsync(ctx);

        using var harness = ServiceHarness.Create(Db, users.AdminId, Roles.Admin);
        var service = harness.Services.GetRequiredService<ILeadService>();

        var created = await service.CreateAsync(new CreateLeadCommand(
            "Delete Me", null, "del@example.com", "9876543210",
            LeadSource.Website, LeadPriority.Low, 1000m, null, null));
        created.Succeeded.ShouldBeTrue();
        var leadId = created.Value!.Id;

        var deleteResult = await service.DeleteAsync(leadId);
        deleteResult.Succeeded.ShouldBeTrue();

        // Verify soft-deleted directly in DB (ignoring query filters)
        await using var verifyCtx = Db.CreateContext();
        var lead = await verifyCtx.Leads.IgnoreQueryFilters().FirstOrDefaultAsync(l => l.Id == leadId);
        lead.ShouldNotBeNull();
        lead.IsDeleted.ShouldBeTrue();
        lead.DeletedBy.ShouldBe(users.AdminId);
        lead.DeletedDate.ShouldNotBeNull();
    }

    [Fact]
    public async Task Search_by_name_company_status_assignee_priority_source()
    {
        await using var ctx = Db.CreateContext();
        var users = await TestUsers.CreateStandardAsync(ctx);

        using var harness = ServiceHarness.Create(Db, users.AdminId, Roles.Admin);
        var service = harness.Services.GetRequiredService<ILeadService>();

        var l1 = await service.CreateAsync(new CreateLeadCommand(
            "UniqueLeadName", "SpecialCorp", "s1@example.com", "9876543210",
            LeadSource.Campaign, LeadPriority.High, 10000m, users.ExecN1Id, null));
        l1.Succeeded.ShouldBeTrue();

        var l2 = await service.CreateAsync(new CreateLeadCommand(
            "OtherLead", "RegularCorp", "s2@example.com", "9876543211",
            LeadSource.Website, LeadPriority.Low, 20000m, users.ExecS1Id, null));
        l2.Succeeded.ShouldBeTrue();

        // Search by name
        var searchName = await service.SearchAsync(new LeadFilter(Search: "UniqueLeadName"), new PageQuery());
        searchName.Items.Count.ShouldBe(1);
        searchName.Items[0].Id.ShouldBe(l1.Value!.Id);

        // Filter by source
        var searchSource = await service.SearchAsync(new LeadFilter(Source: LeadSource.Campaign), new PageQuery());
        searchSource.Items.ShouldContain(x => x.Id == l1.Value!.Id);
        searchSource.Items.ShouldNotContain(x => x.Id == l2.Value!.Id);

        // Filter by priority
        var searchPriority = await service.SearchAsync(new LeadFilter(Priority: LeadPriority.High), new PageQuery());
        searchPriority.Items.ShouldContain(x => x.Id == l1.Value!.Id);
        searchPriority.Items.ShouldNotContain(x => x.Id == l2.Value!.Id);

        // Filter by assignee
        var searchAssignee = await service.SearchAsync(new LeadFilter(OwnerId: users.ExecN1Id), new PageQuery());
        searchAssignee.Items.ShouldContain(x => x.Id == l1.Value!.Id);
        searchAssignee.Items.ShouldNotContain(x => x.Id == l2.Value!.Id);
    }

    [Fact]
    public async Task Search_wildcards_literal()
    {
        await using var ctx = Db.CreateContext();
        var users = await TestUsers.CreateStandardAsync(ctx);

        using var harness = ServiceHarness.Create(Db, users.AdminId, Roles.Admin);
        var service = harness.Services.GetRequiredService<ILeadService>();

        var l1 = await service.CreateAsync(new CreateLeadCommand(
            "100% Guaranteed", "100% Co", "percent@example.com", "9876543210",
            LeadSource.Website, LeadPriority.Medium, 1000m, null, null));
        l1.Succeeded.ShouldBeTrue();

        var l2 = await service.CreateAsync(new CreateLeadCommand(
            "1000 Normal", "Normal Co", "normal@example.com", "9876543211",
            LeadSource.Website, LeadPriority.Medium, 1000m, null, null));
        l2.Succeeded.ShouldBeTrue();

        // Search for literal "%" should only match l1
        var search = await service.SearchAsync(new LeadFilter(Search: "%"), new PageQuery());
        search.Items.ShouldContain(x => x.Id == l1.Value!.Id);
        search.Items.ShouldNotContain(x => x.Id == l2.Value!.Id);
    }

    [Fact]
    public async Task Update_with_stale_rowversion_conflicts()
    {
        await using var ctx = Db.CreateContext();
        var users = await TestUsers.CreateStandardAsync(ctx);

        using var harness = ServiceHarness.Create(Db, users.AdminId, Roles.Admin);
        var service = harness.Services.GetRequiredService<ILeadService>();

        var created = await service.CreateAsync(new CreateLeadCommand(
            "Stale Test", null, "stale@example.com", "9876543210",
            LeadSource.Website, LeadPriority.Low, 1000m, null, null));
        created.Succeeded.ShouldBeTrue();

        var staleVersion = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 };
        var update = await service.UpdateAsync(created.Value!.Id, new UpdateLeadCommand(
            "Stale Test", null, "stale@example.com", "9876543210",
            LeadSource.Website, LeadPriority.Low, 1000m, null, staleVersion));

        update.Succeeded.ShouldBeFalse();
        update.Kind.ShouldBe(ErrorKind.Conflict);
    }
}

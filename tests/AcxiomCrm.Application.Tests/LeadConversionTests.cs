using System.Globalization;
using AcxiomCrm.Application.Common;
using AcxiomCrm.Application.Customers;
using AcxiomCrm.Application.Leads;
using AcxiomCrm.Domain.Audit;
using AcxiomCrm.Domain.Entities;
using AcxiomCrm.Domain.Enums;
using AcxiomCrm.Domain.Security;
using AcxiomCrm.TestSupport;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace AcxiomCrm.Application.Tests;

public class LeadConversionTests : DbTestBase
{
    public LeadConversionTests(SqlServerContainerFixture fixture) : base(fixture)
    {
    }

    [Fact]
    public async Task Qualified_lead_converts_creating_customer_owned_by_lead_owner()
    {
        await using var ctx = Db.CreateContext();
        var users = await TestUsers.CreateStandardAsync(ctx);

        using var harness = ServiceHarness.Create(Db, users.NorthManagerId, Roles.Manager);
        var service = harness.Services.GetRequiredService<ILeadService>();

        // Create lead assigned to ExecN1
        var created = await service.CreateAsync(new CreateLeadCommand(
            "Acme Conversion Lead", "Acme Corp", "acme.lead@example.com", "9876543210",
            LeadSource.Website, LeadPriority.High, 50000m, users.ExecN1Id, "Notes"));
        created.Succeeded.ShouldBeTrue();
        var leadId = created.Value!.Id;

        // Move to Contacted then Qualified
        var s1 = await service.ChangeStatusAsync(leadId, LeadStatus.Contacted, created.Value.RowVersion);
        var s2 = await service.ChangeStatusAsync(leadId, LeadStatus.Qualified, s1.Value!.RowVersion);
        s2.Succeeded.ShouldBeTrue();

        var convertCmd = new ConvertLeadCommand(
            ExistingCustomerId: null,
            CreateOpportunity: false,
            Opportunity: null,
            RowVersion: s2.Value!.RowVersion);

        var result = await service.ConvertAsync(leadId, convertCmd);
        result.Succeeded.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value.LeadId.ShouldBe(leadId);
        result.Value.CustomerId.ShouldBeGreaterThan(0);
        result.Value.OpportunityId.ShouldBeNull();

        // Verify Customer was created in DB and owned by lead owner (ExecN1)
        await using var verifyCtx = Db.CreateContext();
        var customer = await verifyCtx.Customers.FindAsync(result.Value.CustomerId);
        customer.ShouldNotBeNull();
        customer.Name.ShouldBe("Acme Conversion Lead");
        customer.CompanyName.ShouldBe("Acme Corp");
        customer.Email.ShouldBe("acme.lead@example.com");
        customer.Phone.ShouldBe("9876543210");
        customer.OwnerId.ShouldBe(users.ExecN1Id);
        customer.Status.ShouldBe(CustomerStatus.Active);

        // Verify Lead is marked Converted
        var lead = await verifyCtx.Leads.FindAsync(leadId);
        lead.ShouldNotBeNull();
        lead.Status.ShouldBe(LeadStatus.Converted);
        lead.ConvertedCustomerId.ShouldBe(customer.Id);
        lead.ConvertedDate.ShouldNotBeNull();
    }

    [Fact]
    public async Task Converts_linking_existing_in_scope_customer_without_creating()
    {
        await using var ctx = Db.CreateContext();
        var users = await TestUsers.CreateStandardAsync(ctx);

        using var harness = ServiceHarness.Create(Db, users.NorthManagerId, Roles.Manager);
        var custService = harness.Services.GetRequiredService<ICustomerService>();
        var leadService = harness.Services.GetRequiredService<ILeadService>();

        // Create an existing customer
        var custResult = await custService.CreateAsync(new CreateCustomerCommand(
            "Existing Customer", "Existing Corp", "existing@example.com", "9112345678",
            null, null, null, CustomerStatus.Active, users.ExecN1Id, null));
        custResult.Succeeded.ShouldBeTrue();
        var existingCustId = custResult.Value!.Id;

        // Create and qualify lead
        var leadResult = await leadService.CreateAsync(new CreateLeadCommand(
            "Link Lead", "Link Corp", "link.lead@example.com", "9223344556",
            LeadSource.Referral, LeadPriority.Medium, 20000m, users.ExecN1Id, null));
        var s1 = await leadService.ChangeStatusAsync(leadResult.Value!.Id, LeadStatus.Contacted, leadResult.Value.RowVersion);
        var s2 = await leadService.ChangeStatusAsync(leadResult.Value.Id, LeadStatus.Qualified, s1.Value!.RowVersion);

        await using var countCtx = Db.CreateContext();
        var initialCustomerCount = await countCtx.Customers.CountAsync();

        var convertCmd = new ConvertLeadCommand(
            ExistingCustomerId: existingCustId,
            CreateOpportunity: false,
            Opportunity: null,
            RowVersion: s2.Value!.RowVersion);

        var result = await leadService.ConvertAsync(leadResult.Value.Id, convertCmd);
        result.Succeeded.ShouldBeTrue();
        result.Value!.CustomerId.ShouldBe(existingCustId);

        // Verify no new customer was created
        await using var verifyCtx = Db.CreateContext();
        var finalCustomerCount = await verifyCtx.Customers.CountAsync();
        finalCustomerCount.ShouldBe(initialCustomerCount);

        var lead = await verifyCtx.Leads.FindAsync(leadResult.Value.Id);
        lead!.Status.ShouldBe(LeadStatus.Converted);
        lead.ConvertedCustomerId.ShouldBe(existingCustId);
    }

    [Fact]
    public async Task Not_qualified_rejected_with_message()
    {
        await using var ctx = Db.CreateContext();
        var users = await TestUsers.CreateStandardAsync(ctx);

        using var harness = ServiceHarness.Create(Db, users.AdminId, Roles.Admin);
        var service = harness.Services.GetRequiredService<ILeadService>();

        // Lead in New status
        var created = await service.CreateAsync(new CreateLeadCommand(
            "New Lead", null, "new.lead@example.com", "9876543210",
            LeadSource.Website, LeadPriority.Low, 1000m, null, null));
        created.Succeeded.ShouldBeTrue();

        var convertCmd = new ConvertLeadCommand(null, false, null, created.Value!.RowVersion);
        var result = await service.ConvertAsync(created.Value.Id, convertCmd);

        result.Succeeded.ShouldBeFalse();
        result.Kind.ShouldBe(ErrorKind.Conflict);
        result.Errors[0].Message.ShouldBe("Only qualified leads can be converted.");
    }

    [Fact]
    public async Task Missing_contact_on_lead_returns_field_errors()
    {
        await using var ctx = Db.CreateContext();
        var users = await TestUsers.CreateStandardAsync(ctx);

        using var harness = ServiceHarness.Create(Db, users.AdminId, Roles.Admin);
        var service = harness.Services.GetRequiredService<ILeadService>();

        // Lead with phone only (email is null)
        var created = await service.CreateAsync(new CreateLeadCommand(
            "Phone Only Lead", null, null, "9876543210",
            LeadSource.Website, LeadPriority.Low, 1000m, null, null));
        created.Succeeded.ShouldBeTrue();

        var s1 = await service.ChangeStatusAsync(created.Value!.Id, LeadStatus.Contacted, created.Value.RowVersion);
        var s2 = await service.ChangeStatusAsync(created.Value.Id, LeadStatus.Qualified, s1.Value!.RowVersion);

        var convertCmd = new ConvertLeadCommand(null, false, null, s2.Value!.RowVersion);
        var result = await service.ConvertAsync(created.Value.Id, convertCmd);

        result.Succeeded.ShouldBeFalse();
        result.Kind.ShouldBe(ErrorKind.Validation);
        result.Errors.ShouldContain(e => e.Field == "Email" && e.Message == "Add an email address and phone number to the lead before converting.");
    }

    [Fact]
    public async Task Duplicate_customer_returns_conflict_and_suggestion_when_in_scope()
    {
        await using var ctx = Db.CreateContext();
        var users = await TestUsers.CreateStandardAsync(ctx);

        using var harness = ServiceHarness.Create(Db, users.NorthManagerId, Roles.Manager);
        var custService = harness.Services.GetRequiredService<ICustomerService>();
        var leadService = harness.Services.GetRequiredService<ILeadService>();

        // Existing customer in scope (owned by ExecN1)
        var custResult = await custService.CreateAsync(new CreateCustomerCommand(
            "Duplicate Target", "Corp", "shared@example.com", "9112233445",
            null, null, null, CustomerStatus.Active, users.ExecN1Id, null));
        custResult.Succeeded.ShouldBeTrue();

        // Lead has same email
        var leadResult = await leadService.CreateAsync(new CreateLeadCommand(
            "Lead With Duplicate Email", "Corp", "shared@example.com", "9998887776",
            LeadSource.Website, LeadPriority.Medium, 15000m, users.ExecN1Id, null));
        var s1 = await leadService.ChangeStatusAsync(leadResult.Value!.Id, LeadStatus.Contacted, leadResult.Value.RowVersion);
        var s2 = await leadService.ChangeStatusAsync(leadResult.Value.Id, LeadStatus.Qualified, s1.Value!.RowVersion);

        var convertCmd = new ConvertLeadCommand(null, false, null, s2.Value!.RowVersion);
        var result = await leadService.ConvertAsync(leadResult.Value.Id, convertCmd);

        result.Succeeded.ShouldBeFalse();
        result.Kind.ShouldBe(ErrorKind.Conflict);
        result.Errors.ShouldContain(e => e.Message == "A customer with this email already exists. Link to the existing customer instead.");

        // Duplicate customer IS in scope, so FindConvertibleDuplicateCustomerIdAsync returns the id
        var suggestedId = await leadService.FindConvertibleDuplicateCustomerIdAsync(leadResult.Value.Id);
        suggestedId.ShouldBe(custResult.Value!.Id);
    }

    [Fact]
    public async Task Duplicate_out_of_scope_returns_conflict_without_id()
    {
        await using var ctx = Db.CreateContext();
        var users = await TestUsers.CreateStandardAsync(ctx);

        // Create customer owned by South Manager
        using (var southMgrHarness = ServiceHarness.Create(Db, users.SouthManagerId, Roles.Manager))
        {
            var custService = southMgrHarness.Services.GetRequiredService<ICustomerService>();
            var custResult = await custService.CreateAsync(new CreateCustomerCommand(
                "South Manager Customer", "Secret Corp", "manager.only@example.com", "9112233445",
                null, null, null, CustomerStatus.Active, users.SouthManagerId, null));
            custResult.Succeeded.ShouldBeTrue();
        }

        // ExecN1 (under North Manager) creates a lead with the same email
        using var execHarness = ServiceHarness.Create(Db, users.ExecN1Id, Roles.SalesExecutive);
        var leadService = execHarness.Services.GetRequiredService<ILeadService>();

        var leadResult = await leadService.CreateAsync(new CreateLeadCommand(
            "Exec Lead", "Secret Corp", "manager.only@example.com", "9876543210",
            LeadSource.Website, LeadPriority.Medium, 10000m, null, null));
        var s1 = await leadService.ChangeStatusAsync(leadResult.Value!.Id, LeadStatus.Contacted, leadResult.Value.RowVersion);
        var s2 = await leadService.ChangeStatusAsync(leadResult.Value.Id, LeadStatus.Qualified, s1.Value!.RowVersion);

        var convertCmd = new ConvertLeadCommand(null, false, null, s2.Value!.RowVersion);
        var result = await leadService.ConvertAsync(leadResult.Value.Id, convertCmd);

        result.Succeeded.ShouldBeFalse();
        result.Kind.ShouldBe(ErrorKind.Conflict);
        result.Errors.ShouldContain(e => e.Message == "A customer with this email already exists. Link to the existing customer instead.");
        result.Errors.ShouldNotContain(e => e.Message.Contains(users.SouthManagerId));

        // Because South Manager's customer is out of scope for ExecN1, FindConvertibleDuplicateCustomerIdAsync returns null
        var suggestedId = await leadService.FindConvertibleDuplicateCustomerIdAsync(leadResult.Value.Id);
        suggestedId.ShouldBeNull();
    }

    [Fact]
    public async Task Convert_twice_conflicts()
    {
        await using var ctx = Db.CreateContext();
        var users = await TestUsers.CreateStandardAsync(ctx);

        using var harness = ServiceHarness.Create(Db, users.AdminId, Roles.Admin);
        var service = harness.Services.GetRequiredService<ILeadService>();

        var created = await service.CreateAsync(new CreateLeadCommand(
            "Twice Lead", null, "twice@example.com", "9876543210",
            LeadSource.Website, LeadPriority.Low, 1000m, null, null));
        var s1 = await service.ChangeStatusAsync(created.Value!.Id, LeadStatus.Contacted, created.Value.RowVersion);
        var s2 = await service.ChangeStatusAsync(created.Value.Id, LeadStatus.Qualified, s1.Value!.RowVersion);

        var convertCmd = new ConvertLeadCommand(null, false, null, s2.Value!.RowVersion);
        var result1 = await service.ConvertAsync(created.Value.Id, convertCmd);
        result1.Succeeded.ShouldBeTrue();

        // 2nd conversion attempt
        var result2 = await service.ConvertAsync(created.Value.Id, convertCmd);
        result2.Succeeded.ShouldBeFalse();
        result2.Kind.ShouldBe(ErrorKind.Conflict);
    }

    [Fact]
    public async Task Creates_opportunity_when_requested()
    {
        await using var ctx = Db.CreateContext();
        var users = await TestUsers.CreateStandardAsync(ctx);

        using var harness = ServiceHarness.Create(Db, users.AdminId, Roles.Admin);
        var service = harness.Services.GetRequiredService<ILeadService>();

        var created = await service.CreateAsync(new CreateLeadCommand(
            "Opp Lead", "Target Corp", "opp.lead@example.com", "9876543210",
            LeadSource.Website, LeadPriority.Medium, 1000m, null, null));
        var s1 = await service.ChangeStatusAsync(created.Value!.Id, LeadStatus.Contacted, created.Value.RowVersion);
        var s2 = await service.ChangeStatusAsync(created.Value.Id, LeadStatus.Qualified, s1.Value!.RowVersion);

        var oppInput = new ConvertOpportunityInput("Test Opp", 150000m, 20, new DateOnly(2026, 11, 15), "Conversion opp notes");
        var convertCmd = new ConvertLeadCommand(null, true, oppInput, s2.Value!.RowVersion);

        var result = await service.ConvertAsync(created.Value.Id, convertCmd);
        result.Succeeded.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value.OpportunityId.ShouldNotBeNull();
        result.Value.OpportunityId.Value.ShouldBeGreaterThan(0);

        await using var verifyCtx = Db.CreateContext();
        var opp = await verifyCtx.Opportunities.FindAsync(result.Value.OpportunityId.Value);
        opp.ShouldNotBeNull();
        opp.Name.ShouldBe("Test Opp");
        opp.Amount.ShouldBe(150000m);
        opp.Probability.ShouldBe((byte)20);
        opp.ExpectedCloseDate.ShouldBe(new DateOnly(2026, 11, 15));
        opp.CustomerId.ShouldBe(result.Value.CustomerId);
        opp.LeadId.ShouldBe(created.Value.Id);
        opp.Stage.ShouldBe(OpportunityStage.Qualification);
        opp.Status.ShouldBe(OpportunityStatus.Open);
        opp.Notes.ShouldBe("Conversion opp notes");

        var lead = await verifyCtx.Leads.FindAsync(created.Value.Id);
        lead!.Status.ShouldBe(LeadStatus.Converted);
        lead.ConvertedOpportunityId.ShouldBe(opp.Id);
    }

    [Fact]
    public async Task Invalid_opportunity_rolls_back_conversion()
    {
        await using var ctx = Db.CreateContext();
        var users = await TestUsers.CreateStandardAsync(ctx);

        using var harness = ServiceHarness.Create(Db, users.AdminId, Roles.Admin);
        var service = harness.Services.GetRequiredService<ILeadService>();

        var created = await service.CreateAsync(new CreateLeadCommand(
            "Rollback Lead", null, "rollback.lead@example.com", "9876543210",
            LeadSource.Website, LeadPriority.Medium, 1000m, null, null));
        var s1 = await service.ChangeStatusAsync(created.Value!.Id, LeadStatus.Contacted, created.Value.RowVersion);
        var s2 = await service.ChangeStatusAsync(created.Value.Id, LeadStatus.Qualified, s1.Value!.RowVersion);

        // Invalid amount 0
        var oppInput = new ConvertOpportunityInput("Invalid Opp", 0m, 20, new DateOnly(2026, 11, 15), null);
        var convertCmd = new ConvertLeadCommand(null, true, oppInput, s2.Value!.RowVersion);

        var result = await service.ConvertAsync(created.Value.Id, convertCmd);
        result.Succeeded.ShouldBeFalse();
        result.Kind.ShouldBe(ErrorKind.Validation);

        await using var verifyCtx = Db.CreateContext();
        var lead = await verifyCtx.Leads.FindAsync(created.Value.Id);
        lead!.Status.ShouldBe(LeadStatus.Qualified);
        lead.ConvertedCustomerId.ShouldBeNull();

        var customer = await verifyCtx.Customers.FirstOrDefaultAsync(c => c.Email == "rollback.lead@example.com");
        customer.ShouldBeNull();

        var opp = await verifyCtx.Opportunities.FirstOrDefaultAsync(o => o.Name == "Invalid Opp");
        opp.ShouldBeNull();
    }

    [Fact]
    public async Task Executive_cannot_convert_other_executives_lead()
    {
        await using var ctx = Db.CreateContext();
        var users = await TestUsers.CreateStandardAsync(ctx);

        // ExecN1 creates and qualifies a lead
        int leadId;
        byte[] rowVersion;
        using (var exec1Harness = ServiceHarness.Create(Db, users.ExecN1Id, Roles.SalesExecutive))
        {
            var service = exec1Harness.Services.GetRequiredService<ILeadService>();
            var created = await service.CreateAsync(new CreateLeadCommand(
                "Exec1 Lead", null, "exec1.lead@example.com", "9876543210",
                LeadSource.Website, LeadPriority.Low, 1000m, null, null));
            var s1 = await service.ChangeStatusAsync(created.Value!.Id, LeadStatus.Contacted, created.Value.RowVersion);
            var s2 = await service.ChangeStatusAsync(created.Value.Id, LeadStatus.Qualified, s1.Value!.RowVersion);
            leadId = created.Value.Id;
            rowVersion = s2.Value!.RowVersion;
        }

        // ExecS1 (different executive) tries to convert ExecN1's lead
        using (var exec2Harness = ServiceHarness.Create(Db, users.ExecS1Id, Roles.SalesExecutive))
        {
            var service = exec2Harness.Services.GetRequiredService<ILeadService>();
            var convertCmd = new ConvertLeadCommand(null, false, null, rowVersion);
            var result = await service.ConvertAsync(leadId, convertCmd);

            result.Succeeded.ShouldBeFalse();
            result.Kind.ShouldBe(ErrorKind.NotFound);
        }
    }

    [Fact]
    public async Task Writes_Converted_audit_with_ids()
    {
        await using var ctx = Db.CreateContext();
        var users = await TestUsers.CreateStandardAsync(ctx);

        using var harness = ServiceHarness.Create(Db, users.AdminId, Roles.Admin);
        var service = harness.Services.GetRequiredService<ILeadService>();

        var created = await service.CreateAsync(new CreateLeadCommand(
            "Audit Convert Lead", "Audit Corp", "audit.convert@example.com", "9876543210",
            LeadSource.Website, LeadPriority.Medium, 10000m, null, null));
        var s1 = await service.ChangeStatusAsync(created.Value!.Id, LeadStatus.Contacted, created.Value.RowVersion);
        var s2 = await service.ChangeStatusAsync(created.Value.Id, LeadStatus.Qualified, s1.Value!.RowVersion);

        var convertCmd = new ConvertLeadCommand(null, false, null, s2.Value!.RowVersion);
        var result = await service.ConvertAsync(created.Value.Id, convertCmd);
        result.Succeeded.ShouldBeTrue();

        await using var verifyCtx = Db.CreateContext();
        var audits = await verifyCtx.AuditLogs
            .Where(a => a.EntityName == nameof(Lead) && a.RecordId == created.Value.Id.ToString(CultureInfo.InvariantCulture))
            .OrderByDescending(a => a.AuditLogId)
            .ToListAsync();

        var convertedAudit = audits.FirstOrDefault(a => a.Action == AuditActions.Converted);
        convertedAudit.ShouldNotBeNull();
        convertedAudit.Module.ShouldBe(AuditModules.Leads);
        convertedAudit.NewValue.ShouldNotBeNull();
        convertedAudit.NewValue.ShouldContain("customerId");
        convertedAudit.NewValue.ShouldContain(result.Value!.CustomerId.ToString(CultureInfo.InvariantCulture));
    }

    [Fact]
    public async Task Lead_becomes_read_only_after_conversion()
    {
        await using var ctx = Db.CreateContext();
        var users = await TestUsers.CreateStandardAsync(ctx);

        using var harness = ServiceHarness.Create(Db, users.AdminId, Roles.Admin);
        var service = harness.Services.GetRequiredService<ILeadService>();

        var created = await service.CreateAsync(new CreateLeadCommand(
            "ReadOnly Lead", "ReadOnly Corp", "readonly@example.com", "9876543210",
            LeadSource.Website, LeadPriority.Medium, 10000m, null, null));
        var s1 = await service.ChangeStatusAsync(created.Value!.Id, LeadStatus.Contacted, created.Value.RowVersion);
        var s2 = await service.ChangeStatusAsync(created.Value.Id, LeadStatus.Qualified, s1.Value!.RowVersion);

        var convertCmd = new ConvertLeadCommand(null, false, null, s2.Value!.RowVersion);
        var result = await service.ConvertAsync(created.Value.Id, convertCmd);
        result.Succeeded.ShouldBeTrue();

        // Updating details should now be rejected
        var updateCmd = new UpdateLeadCommand(
            "Modified Name", "ReadOnly Corp", "readonly@example.com", "9876543210",
            LeadSource.Website, LeadPriority.Medium, 10000m, null, s2.Value.RowVersion);

        var updateResult = await service.UpdateAsync(created.Value.Id, updateCmd);
        updateResult.Succeeded.ShouldBeFalse();
        updateResult.Kind.ShouldBe(ErrorKind.Conflict);
        updateResult.Errors[0].Message.ShouldContain("cannot be edited");
    }
}

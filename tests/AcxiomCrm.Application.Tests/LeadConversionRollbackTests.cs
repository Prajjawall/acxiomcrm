using System.Globalization;
using AcxiomCrm.Application.Common;
using AcxiomCrm.Application.Leads;
using AcxiomCrm.Domain.Audit;
using AcxiomCrm.Domain.Entities;
using AcxiomCrm.Domain.Enums;
using AcxiomCrm.Domain.Security;
using AcxiomCrm.Domain.Validation;
using AcxiomCrm.TestSupport;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace AcxiomCrm.Application.Tests;

public class LeadConversionRollbackTests : DbTestBase
{
    public LeadConversionRollbackTests(SqlServerContainerFixture fixture) : base(fixture)
    {
    }

    private sealed class FailingOpportunityFactory : IOpportunityFactory
    {
        public bool IsAvailable => true;

        public Task<Result<int>> CreateForConversionAsync(
            int customerId,
            int leadId,
            string ownerId,
            ConvertOpportunityInput input,
            CancellationToken ct = default)
        {
            return Task.FromResult(Result<int>.Fail(ErrorKind.Validation, [new FieldError("Amount", "Opportunity Amount must be greater than 0.")]));
        }
    }

    private sealed class ThrowingSaveChangesInterceptor : SaveChangesInterceptor
    {
        public bool ShouldThrow { get; set; }

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            if (ShouldThrow)
            {
                throw new InvalidOperationException("Simulated DB failure during SaveChanges.");
            }
            return base.SavingChangesAsync(eventData, result, cancellationToken);
        }
    }

    [Fact]
    public async Task Failing_opportunity_factory_rolls_back_conversion()
    {
        await using var ctx = Db.CreateContext();
        var users = await TestUsers.CreateStandardAsync(ctx);

        using var harness = ServiceHarness.Create(Db, users.AdminId, Roles.Admin, configureServices: services =>
        {
            services.AddScoped<IOpportunityFactory, FailingOpportunityFactory>();
        });
        var service = harness.Services.GetRequiredService<ILeadService>();

        // Create and qualify lead
        var created = await service.CreateAsync(new CreateLeadCommand(
            "Rollback Lead 1", "Rollback Corp", "rollback1@example.com", "9876543210",
            LeadSource.Website, LeadPriority.High, 25000m, null, null));
        var s1 = await service.ChangeStatusAsync(created.Value!.Id, LeadStatus.Contacted, created.Value.RowVersion);
        var s2 = await service.ChangeStatusAsync(created.Value.Id, LeadStatus.Qualified, s1.Value!.RowVersion);

        await using var countCtx = Db.CreateContext();
        var initialCustomerCount = await countCtx.Customers.CountAsync();

        var oppInput = new ConvertOpportunityInput("Invalid Opp", 0m, 10, new DateOnly(2026, 11, 15), null);
        var convertCmd = new ConvertLeadCommand(null, true, oppInput, s2.Value!.RowVersion);

        var result = await service.ConvertAsync(created.Value.Id, convertCmd);
        result.Succeeded.ShouldBeFalse();

        // Verify rollback:
        await using var verifyCtx = Db.CreateContext();
        var finalCustomerCount = await verifyCtx.Customers.CountAsync();
        finalCustomerCount.ShouldBe(initialCustomerCount);

        var lead = await verifyCtx.Leads.FindAsync(created.Value.Id);
        lead.ShouldNotBeNull();
        lead.Status.ShouldBe(LeadStatus.Qualified);
        lead.ConvertedCustomerId.ShouldBeNull();

        var convertedAudit = await verifyCtx.AuditLogs
            .Where(a => a.EntityName == nameof(Lead) &&
                        a.RecordId == created.Value.Id.ToString(CultureInfo.InvariantCulture) &&
                        a.Action == AuditActions.Converted)
            .FirstOrDefaultAsync();
        convertedAudit.ShouldBeNull();
    }

    [Fact]
    public async Task Db_exception_rolls_back_conversion()
    {
        await using var ctx = Db.CreateContext();
        var users = await TestUsers.CreateStandardAsync(ctx);

        var throwingInterceptor = new ThrowingSaveChangesInterceptor();

        using var harness = ServiceHarness.Create(Db, users.AdminId, Roles.Admin, configureServices: services =>
        {
            services.AddSingleton<IInterceptor>(throwingInterceptor);
        });
        var service = harness.Services.GetRequiredService<ILeadService>();

        // Create and qualify lead with throwing disabled initially
        var created = await service.CreateAsync(new CreateLeadCommand(
            "Rollback Lead 2", "Rollback Corp", "rollback2@example.com", "9876543210",
            LeadSource.Website, LeadPriority.High, 25000m, null, null));
        var s1 = await service.ChangeStatusAsync(created.Value!.Id, LeadStatus.Contacted, created.Value.RowVersion);
        var s2 = await service.ChangeStatusAsync(created.Value.Id, LeadStatus.Qualified, s1.Value!.RowVersion);

        await using var countCtx = Db.CreateContext();
        var initialCustomerCount = await countCtx.Customers.CountAsync();

        // Enable throwing on SaveChanges
        throwingInterceptor.ShouldThrow = true;

        var convertCmd = new ConvertLeadCommand(null, false, null, s2.Value!.RowVersion);

        // ConvertAsync should fail or catch and return Fail
        try
        {
            var result = await service.ConvertAsync(created.Value.Id, convertCmd);
            result.Succeeded.ShouldBeFalse();
        }
        catch (InvalidOperationException)
        {
            // Even if exception bubbled up, transaction must have rolled back
        }

        // Verify rollback
        throwingInterceptor.ShouldThrow = false;

        await using var verifyCtx = Db.CreateContext();
        var finalCustomerCount = await verifyCtx.Customers.CountAsync();
        finalCustomerCount.ShouldBe(initialCustomerCount);

        var lead = await verifyCtx.Leads.FindAsync(created.Value.Id);
        lead.ShouldNotBeNull();
        lead.Status.ShouldBe(LeadStatus.Qualified);
        lead.ConvertedCustomerId.ShouldBeNull();

        var convertedAudit = await verifyCtx.AuditLogs
            .Where(a => a.EntityName == nameof(Lead) &&
                        a.RecordId == created.Value.Id.ToString(CultureInfo.InvariantCulture) &&
                        a.Action == AuditActions.Converted)
            .FirstOrDefaultAsync();
        convertedAudit.ShouldBeNull();
    }
}

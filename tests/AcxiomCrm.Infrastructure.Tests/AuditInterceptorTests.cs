using System.Globalization;
using AcxiomCrm.Domain.Audit;
using AcxiomCrm.Domain.Entities;
using AcxiomCrm.Infrastructure.Audit;
using AcxiomCrm.TestSupport;
using AcxiomCrm.TestSupport.Fakes;
using Microsoft.EntityFrameworkCore;
using Shouldly;

namespace AcxiomCrm.Infrastructure.Tests;

public class AuditInterceptorTests : DbTestBase
{
    private readonly DateTime _fixedTime = new(2026, 10, 17, 12, 0, 0, DateTimeKind.Utc);

    public AuditInterceptorTests(SqlServerContainerFixture fixture) : base(fixture)
    {
    }

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();

        // Create table for AuditedThing in the test database
        await using var ctx = Db.CreateContext();
        await ctx.Database.ExecuteSqlAsync($@"
            IF OBJECT_ID('dbo.AuditedThings') IS NULL
            BEGIN
                CREATE TABLE dbo.AuditedThings (
                    Id BIGINT IDENTITY(1,1) PRIMARY KEY,
                    Name NVARCHAR(100) NOT NULL,
                    Description NVARCHAR(500) NULL,
                    PasswordHash NVARCHAR(200) NULL,
                    RowVersion ROWVERSION,
                    CreatedDate DATETIME2(7) NOT NULL,
                    CreatedBy NVARCHAR(450) NOT NULL,
                    ModifiedDate DATETIME2(7) NULL,
                    ModifiedBy NVARCHAR(450) NULL,
                    IsDeleted BIT NOT NULL,
                    DeletedDate DATETIME2(7) NULL,
                    DeletedBy NVARCHAR(450) NULL,
                    OwnerId NVARCHAR(450) NOT NULL
                );
            END
        ");
    }

    private TestCrmDbContext CreateContext(FakeCurrentUser? user = null, FakeClock? clock = null)
    {
        user ??= new FakeCurrentUser("test-user", "SalesExecutive", "test.user", "127.0.0.1", "corr-1");
        clock ??= new FakeClock(_fixedTime);

        var interceptor = new AuditSaveChangesInterceptor(clock, user);

        var options = new DbContextOptionsBuilder<TestCrmDbContext>()
            .UseSqlServer(Db.ConnectionString)
            .AddInterceptors(interceptor)
            .Options;

        return new TestCrmDbContext(options);
    }

    [Fact]
    public async Task Create_stamps_and_writes_audit_with_generated_id()
    {
        await using var ctx = CreateContext();

        var thing = new AuditedThing
        {
            Name = "Widget A",
            Description = "Initial description",
            OwnerId = "owner-1"
        };

        ctx.AuditedThings.Add(thing);
        await ctx.SaveChangesAsync();

        thing.Id.ShouldBeGreaterThan(0);
        thing.CreatedDate.ShouldBe(_fixedTime);
        thing.CreatedBy.ShouldBe("test-user");

        var log = await ctx.AuditLogs
            .Where(a => a.EntityName == "AuditedThing" && a.RecordId == thing.Id.ToString(CultureInfo.InvariantCulture))
            .SingleOrDefaultAsync();

        log.ShouldNotBeNull();
        log.Action.ShouldBe(AuditActions.Create);
        log.UserId.ShouldBe("test-user");
        log.UserName.ShouldBe("test.user");
        log.NewValue.ShouldNotBeNull();
        log.NewValue.ShouldContain("Widget A");
    }

    [Fact]
    public async Task Update_writes_only_changed_properties_old_and_new()
    {
        long id;
        await using (var ctx = CreateContext())
        {
            var thing = new AuditedThing { Name = "Original Name", Description = "Old Desc", OwnerId = "owner-1" };
            ctx.AuditedThings.Add(thing);
            await ctx.SaveChangesAsync();
            id = thing.Id;
        }

        var newTime = _fixedTime.AddHours(1);
        var clock = new FakeClock(newTime);

        await using (var ctx = CreateContext(clock: clock))
        {
            var thing = await ctx.AuditedThings.FindAsync(id);
            thing!.Description = "New Desc";
            await ctx.SaveChangesAsync();

            thing.ModifiedDate.ShouldBe(newTime);
            thing.ModifiedBy.ShouldBe("test-user");

            var log = await ctx.AuditLogs
                .Where(a => a.EntityName == "AuditedThing" && a.RecordId == id.ToString(CultureInfo.InvariantCulture) && a.Action == AuditActions.Update)
                .SingleOrDefaultAsync();

            log.ShouldNotBeNull();
            log.OldValue.ShouldNotBeNull();
            log.OldValue.ShouldContain("Old Desc");
            log.NewValue.ShouldNotBeNull();
            log.NewValue.ShouldContain("New Desc");
            // Name did not change, so it should not be in the update diff
            log.OldValue.ShouldNotContain("Original Name");
            log.NewValue.ShouldNotContain("Original Name");
        }
    }

    [Fact]
    public async Task Update_with_no_real_change_writes_nothing()
    {
        long id;
        await using (var ctx = CreateContext())
        {
            var thing = new AuditedThing { Name = "Static Name", OwnerId = "owner-1" };
            ctx.AuditedThings.Add(thing);
            await ctx.SaveChangesAsync();
            id = thing.Id;
        }

        await using (var ctx = CreateContext())
        {
            var initialLogCount = await ctx.AuditLogs.CountAsync();
            var thing = await ctx.AuditedThings.FindAsync(id);
            thing!.Name = "Static Name"; // No actual change
            await ctx.SaveChangesAsync();

            var finalLogCount = await ctx.AuditLogs.CountAsync();
            finalLogCount.ShouldBe(initialLogCount);
        }
    }

    [Fact]
    public async Task Modified_timestamps_only_change_writes_nothing()
    {
        long id;
        await using (var ctx = CreateContext())
        {
            var thing = new AuditedThing { Name = "Timestamp Test", OwnerId = "owner-1" };
            ctx.AuditedThings.Add(thing);
            await ctx.SaveChangesAsync();
            id = thing.Id;
        }

        await using (var ctx = CreateContext())
        {
            var initialLogCount = await ctx.AuditLogs.CountAsync();
            var thing = await ctx.AuditedThings.FindAsync(id);
            thing!.ModifiedDate = _fixedTime.AddDays(1);
            thing.ModifiedBy = "someone";
            await ctx.SaveChangesAsync();

            var finalLogCount = await ctx.AuditLogs.CountAsync();
            finalLogCount.ShouldBe(initialLogCount);
        }
    }

    [Fact]
    public async Task Remove_soft_deletes_and_audits_Delete()
    {
        long id;
        await using (var ctx = CreateContext())
        {
            var thing = new AuditedThing { Name = "To be deleted", OwnerId = "owner-1" };
            ctx.AuditedThings.Add(thing);
            await ctx.SaveChangesAsync();
            id = thing.Id;
        }

        var deleteTime = _fixedTime.AddHours(2);
        var clock = new FakeClock(deleteTime);

        await using (var ctx = CreateContext(clock: clock))
        {
            var thing = await ctx.AuditedThings.FindAsync(id);
            ctx.AuditedThings.Remove(thing!);
            await ctx.SaveChangesAsync();

            // Check database raw row: should be soft deleted
            var rawRow = await ctx.AuditedThings.IgnoreQueryFilters().SingleAsync(x => x.Id == id);
            rawRow.IsDeleted.ShouldBeTrue();
            rawRow.DeletedDate.ShouldBe(deleteTime);
            rawRow.DeletedBy.ShouldBe("test-user");

            var log = await ctx.AuditLogs
                .Where(a => a.EntityName == "AuditedThing" && a.RecordId == id.ToString(CultureInfo.InvariantCulture) && a.Action == AuditActions.Delete)
                .SingleOrDefaultAsync();

            log.ShouldNotBeNull();
            log.OldValue.ShouldNotBeNull();
            log.OldValue.ShouldContain("To be deleted");
        }
    }

    [Fact]
    public async Task Sensitive_properties_never_captured()
    {
        await using var ctx = CreateContext();

        var thing = new AuditedThing
        {
            Name = "Secret Account",
            PasswordHash = "SuperSecretHash123",
            OwnerId = "owner-1"
        };

        ctx.AuditedThings.Add(thing);
        await ctx.SaveChangesAsync();

        var logs = await ctx.AuditLogs.Where(a => a.RecordId == thing.Id.ToString(CultureInfo.InvariantCulture)).ToListAsync();
        logs.ShouldNotBeEmpty();

        foreach (var l in logs)
        {
            if (l.OldValue != null)
            {
                l.OldValue.ShouldNotContain("SuperSecretHash123");
                l.OldValue.ShouldNotContain("PasswordHash");
            }
            if (l.NewValue != null)
            {
                l.NewValue.ShouldNotContain("SuperSecretHash123");
                l.NewValue.ShouldNotContain("PasswordHash");
            }
        }
    }

    [Fact]
    public async Task Rollback_leaves_no_audit_rows()
    {
        await using var ctx = CreateContext();

        await using (var tx = await ctx.Database.BeginTransactionAsync())
        {
            var thing = new AuditedThing { Name = "Rollback Item", OwnerId = "owner-1" };
            ctx.AuditedThings.Add(thing);
            await ctx.SaveChangesAsync();

            await tx.RollbackAsync();
        }

        var things = await ctx.AuditedThings.Where(x => x.Name == "Rollback Item").ToListAsync();
        things.ShouldBeEmpty();

        var logs = await ctx.AuditLogs.Where(a => a.NewValue != null && a.NewValue.Contains("Rollback Item")).ToListAsync();
        logs.ShouldBeEmpty();
    }

    [Fact]
    public async Task Updating_an_AuditLog_row_throws_InvalidOperationException_before_hitting_the_database()
    {
        await using var ctx = CreateContext();

        var log = new AuditLog
        {
            Action = AuditActions.Create,
            CreatedDate = _fixedTime,
            EntityName = "Test",
            Module = "Test",
            Result = Domain.Enums.AuditResult.Success
        };

        ctx.AuditLogs.Add(log);
        await ctx.SaveChangesAsync();

        log.Details = "Tampered details";

        var ex = Should.Throw<InvalidOperationException>(() => ctx.SaveChanges());
        ex.Message.ShouldContain("cannot be modified or deleted");
    }

    [Fact]
    public async Task Concurrent_users_are_attributed_correctly()
    {
        var user1 = new FakeCurrentUser("user-1", "SalesExecutive", "rajesh", "10.0.0.1", "c1");
        var user2 = new FakeCurrentUser("user-2", "Manager", "priya", "10.0.0.2", "c2");

        await using var ctx1 = CreateContext(user: user1);
        await using var ctx2 = CreateContext(user: user2);

        var thing1 = new AuditedThing { Name = "Thing User 1", OwnerId = "user-1" };
        var thing2 = new AuditedThing { Name = "Thing User 2", OwnerId = "user-2" };

        ctx1.AuditedThings.Add(thing1);
        ctx2.AuditedThings.Add(thing2);

        await ctx1.SaveChangesAsync();
        await ctx2.SaveChangesAsync();

        var log1 = await ctx1.AuditLogs.SingleAsync(a => a.RecordId == thing1.Id.ToString(CultureInfo.InvariantCulture));
        var log2 = await ctx2.AuditLogs.SingleAsync(a => a.RecordId == thing2.Id.ToString(CultureInfo.InvariantCulture));

        log1.UserId.ShouldBe("user-1");
        log1.UserName.ShouldBe("rajesh");

        log2.UserId.ShouldBe("user-2");
        log2.UserName.ShouldBe("priya");
    }

    [Fact]
    public async Task Anonymous_system_changes_use_system_user_id_literal_system()
    {
        var anonymousUser = new FakeCurrentUser();
        await using var ctx = CreateContext(user: anonymousUser);

        var thing = new AuditedThing { Name = "Anonymous Thing", OwnerId = "system" };
        ctx.AuditedThings.Add(thing);
        await ctx.SaveChangesAsync();

        thing.CreatedBy.ShouldBe("system");

        var log = await ctx.AuditLogs.SingleAsync(a => a.RecordId == thing.Id.ToString(CultureInfo.InvariantCulture));
        log.UserId.ShouldBe("system");
    }
}

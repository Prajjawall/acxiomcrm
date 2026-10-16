using AcxiomCrm.Application.Abstractions;
using AcxiomCrm.Domain.Audit;
using AcxiomCrm.Domain.Enums;
using AcxiomCrm.Infrastructure.Audit;
using AcxiomCrm.Infrastructure.Identity;
using AcxiomCrm.TestSupport;
using Microsoft.EntityFrameworkCore;
using Shouldly;

namespace AcxiomCrm.Infrastructure.Tests;

public class AuditWriterTests : DbTestBase
{
    public AuditWriterTests(SqlServerContainerFixture fixture) : base(fixture)
    {
    }

    [Fact]
    public async Task Writes_row_with_user_clock_ip_and_correlation_id()
    {
        var fixedTime = new DateTime(2026, 10, 17, 10, 0, 0, DateTimeKind.Utc);
        var clock = new FakeClock(fixedTime);
        var user = new FakeCurrentUser("user-123", "SalesExecutive", "rajesh.sharma", "192.168.1.1", "corr-123");

        var factory = new SimpleDbContextFactory(Db.ConnectionString);
        var writer = new AuditWriter(factory, user, clock);

        var entry = new AuditEntry(
            Action: AuditActions.Create,
            EntityName: "Customer",
            RecordId: "1",
            Result: AuditResult.Success,
            Details: "Created customer"
        );

        await writer.WriteAsync(entry);

        await using var ctx = Db.CreateContext();
        var row = await ctx.AuditLogs.SingleAsync();

        row.Action.ShouldBe(AuditActions.Create);
        row.EntityName.ShouldBe("Customer");
        row.Module.ShouldBe(AuditModules.Customers);
        row.RecordId.ShouldBe("1");
        row.Result.ShouldBe(AuditResult.Success);
        row.UserId.ShouldBe("user-123");
        row.UserName.ShouldBe("rajesh.sharma");
        row.IpAddress.ShouldBe("192.168.1.1");
        row.CorrelationId.ShouldBe("corr-123");
        row.CreatedDate.ShouldBe(fixedTime);
    }

    [Fact]
    public async Task Anonymous_event_has_null_user()
    {
        var clock = new FakeClock();
        var user = new FakeCurrentUser(); // anonymous

        var factory = new SimpleDbContextFactory(Db.ConnectionString);
        var writer = new AuditWriter(factory, user, clock);

        var entry = new AuditEntry(
            Action: AuditActions.LoginFailed,
            EntityName: "User",
            RecordId: null,
            Result: AuditResult.Failure,
            Details: "Invalid credentials"
        );

        await writer.WriteAsync(entry);

        await using var ctx = Db.CreateContext();
        var row = await ctx.AuditLogs.SingleAsync();

        row.UserId.ShouldBeNull();
        row.UserName.ShouldBeNull();
        row.Module.ShouldBe(AuditModules.Authentication);
    }

    [Fact]
    public async Task UserIdOverride_wins_and_UserName_is_looked_up()
    {
        await using (var seedCtx = Db.CreateContext())
        {
            seedCtx.Users.Add(new ApplicationUser
            {
                Id = "override-user-id",
                UserName = "override.user",
                NormalizedUserName = "OVERRIDE.USER",
                Email = "override@example.com",
                NormalizedEmail = "OVERRIDE@EXAMPLE.COM",
                FullName = "Override User",
                CreatedDate = new DateTime(2026, 10, 16, 0, 0, 0, DateTimeKind.Utc)
            });
            await seedCtx.SaveChangesAsync();
        }

        var clock = new FakeClock();
        var user = new FakeCurrentUser(); // anonymous

        var factory = new SimpleDbContextFactory(Db.ConnectionString);
        var writer = new AuditWriter(factory, user, clock);

        var entry = new AuditEntry(
            Action: AuditActions.Login,
            EntityName: "User",
            RecordId: "override-user-id",
            Result: AuditResult.Success,
            UserIdOverride: "override-user-id"
        );

        await writer.WriteAsync(entry);

        await using var ctx = Db.CreateContext();
        var row = await ctx.AuditLogs.SingleAsync();

        row.UserId.ShouldBe("override-user-id");
        row.UserName.ShouldBe("override.user");
    }

    [Fact]
    public async Task Details_truncated_to_1000_with_ellipsis()
    {
        var clock = new FakeClock();
        var user = new FakeCurrentUser();
        var factory = new SimpleDbContextFactory(Db.ConnectionString);
        var writer = new AuditWriter(factory, user, clock);

        var longDetails = new string('A', 1500);

        var entry = new AuditEntry(
            Action: AuditActions.Create,
            EntityName: "Customer",
            RecordId: "1",
            Result: AuditResult.Success,
            Details: longDetails
        );

        await writer.WriteAsync(entry);

        await using var ctx = Db.CreateContext();
        var row = await ctx.AuditLogs.SingleAsync();

        row.Details!.Length.ShouldBe(1000);
        row.Details.ShouldEndWith("...");
    }

    [Fact]
    public async Task Failure_event_persists_even_when_callers_transaction_rolls_back()
    {
        var clock = new FakeClock();
        var user = new FakeCurrentUser();
        var factory = new SimpleDbContextFactory(Db.ConnectionString);
        var writer = new AuditWriter(factory, user, clock);

        await using (var callerCtx = Db.CreateContext())
        await using (var tx = await callerCtx.BeginTransactionAsync())
        {
            var entry = new AuditEntry(
                Action: AuditActions.LoginFailed,
                EntityName: "User",
                RecordId: null,
                Result: AuditResult.Failure,
                Details: "Login attempt failed"
            );

            await writer.WriteAsync(entry);

            await tx.RollbackAsync();
        }

        await using var verifyCtx = Db.CreateContext();
        var count = await verifyCtx.AuditLogs.CountAsync();
        count.ShouldBe(1);
    }

    [Fact]
    public async Task Sensitive_keys_in_Details_are_not_accepted()
    {
        var clock = new FakeClock();
        var user = new FakeCurrentUser();
        var factory = new SimpleDbContextFactory(Db.ConnectionString);
        var writer = new AuditWriter(factory, user, clock);

        var entry = new AuditEntry(
            Action: AuditActions.LoginFailed,
            EntityName: "User",
            RecordId: null,
            Result: AuditResult.Failure,
            Details: "Attempt with password=SecretPassword123"
        );

        await Should.ThrowAsync<ArgumentException>(() => writer.WriteAsync(entry));
    }

    private sealed class SimpleDbContextFactory : IDbContextFactory<Persistence.CrmDbContext>
    {
        private readonly string _connectionString;

        public SimpleDbContextFactory(string connectionString)
        {
            _connectionString = connectionString;
        }

        public Persistence.CrmDbContext CreateDbContext()
        {
            var options = new DbContextOptionsBuilder<Persistence.CrmDbContext>()
                .UseSqlServer(_connectionString)
                .Options;

            return new Persistence.CrmDbContext(options);
        }

        public Task<Persistence.CrmDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(CreateDbContext());
        }
    }
}

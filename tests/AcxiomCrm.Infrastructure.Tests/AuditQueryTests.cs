using System.Globalization;
using AcxiomCrm.Application.Audit;
using AcxiomCrm.Application.Common;
using AcxiomCrm.Domain.Audit;
using AcxiomCrm.Domain.Entities;
using AcxiomCrm.Domain.Enums;
using AcxiomCrm.Infrastructure.Audit;
using AcxiomCrm.Infrastructure.Options;
using AcxiomCrm.TestSupport;
using Shouldly;

namespace AcxiomCrm.Infrastructure.Tests;

public class AuditQueryTests : DbTestBase
{
    public AuditQueryTests(SqlServerContainerFixture fixture) : base(fixture)
    {
    }

    private AuditQueryService CreateService(string role = "Admin", string? userId = "admin-1")
    {
        var context = Db.CreateContext();
        var user = new FakeCurrentUser(userId ?? "user-1", role, "admin.user", "127.0.0.1", "c1");
        var options = Microsoft.Extensions.Options.Options.Create(new BusinessOptions { TimeZone = "Asia/Kolkata" });
        return new AuditQueryService(context, user, options);
    }

    [Fact]
    public async Task Filters_by_user_module_action_result_entity_record()
    {
        var service = CreateService();

        await using (var ctx = Db.CreateContext())
        {
            ctx.AuditLogs.AddRange(
                new AuditLog
                {
                    CreatedDate = new DateTime(2026, 10, 17, 10, 0, 0, DateTimeKind.Utc),
                    UserId = "user-a",
                    UserName = "User A",
                    Module = AuditModules.Customers,
                    Action = AuditActions.Create,
                    EntityName = "Customer",
                    RecordId = "101",
                    Result = AuditResult.Success,
                    Details = "Created customer 101"
                },
                new AuditLog
                {
                    CreatedDate = new DateTime(2026, 10, 17, 11, 0, 0, DateTimeKind.Utc),
                    UserId = "user-b",
                    UserName = "User B",
                    Module = AuditModules.Leads,
                    Action = AuditActions.Update,
                    EntityName = "Lead",
                    RecordId = "202",
                    Result = AuditResult.Failure,
                    Details = "Failed lead update"
                }
            );
            await ctx.SaveChangesAsync();
        }

        var filter = new AuditFilter(
            UserId: "user-a",
            Module: AuditModules.Customers,
            Action: AuditActions.Create,
            Result: AuditResult.Success,
            EntityName: "Customer",
            RecordId: "101"
        );

        var result = await service.SearchAsync(filter, new PageQuery(1, 10));

        result.Total.ShouldBe(1);
        result.Items.Count.ShouldBe(1);
        result.Items[0].RecordId.ShouldBe("101");
        result.Items[0].UserName.ShouldBe("User A");
    }

    [Fact]
    public async Task Date_range_uses_business_days_inclusive()
    {
        var service = CreateService();

        // 18:31 UTC on 2026-10-16 corresponds to 00:01 IST on 2026-10-17
        var inRangeDate = new DateTime(2026, 10, 16, 18, 31, 0, DateTimeKind.Utc);
        // 18:29 UTC on 2026-10-16 corresponds to 23:59 IST on 2026-10-16 (out of range)
        var outOfRangeDate = new DateTime(2026, 10, 16, 18, 29, 0, DateTimeKind.Utc);

        await using (var ctx = Db.CreateContext())
        {
            ctx.AuditLogs.AddRange(
                new AuditLog
                {
                    CreatedDate = inRangeDate,
                    Module = AuditModules.Customers,
                    Action = AuditActions.Create,
                    EntityName = "Customer",
                    RecordId = "in-range",
                    Result = AuditResult.Success
                },
                new AuditLog
                {
                    CreatedDate = outOfRangeDate,
                    Module = AuditModules.Customers,
                    Action = AuditActions.Create,
                    EntityName = "Customer",
                    RecordId = "out-of-range",
                    Result = AuditResult.Success
                }
            );
            await ctx.SaveChangesAsync();
        }

        var date = new DateOnly(2026, 10, 17);
        var filter = new AuditFilter(From: date, To: date);

        var result = await service.SearchAsync(filter, new PageQuery(1, 10));

        result.Items.ShouldContain(x => x.RecordId == "in-range");
        result.Items.ShouldNotContain(x => x.RecordId == "out-of-range");
    }

    [Fact]
    public async Task Sorted_newest_first_by_default()
    {
        var service = CreateService();

        await using (var ctx = Db.CreateContext())
        {
            ctx.AuditLogs.AddRange(
                new AuditLog
                {
                    CreatedDate = new DateTime(2026, 10, 17, 8, 0, 0, DateTimeKind.Utc),
                    Module = AuditModules.Customers,
                    Action = AuditActions.Create,
                    EntityName = "Customer",
                    RecordId = "first",
                    Result = AuditResult.Success
                },
                new AuditLog
                {
                    CreatedDate = new DateTime(2026, 10, 17, 12, 0, 0, DateTimeKind.Utc),
                    Module = AuditModules.Customers,
                    Action = AuditActions.Create,
                    EntityName = "Customer",
                    RecordId = "second",
                    Result = AuditResult.Success
                }
            );
            await ctx.SaveChangesAsync();
        }

        var result = await service.SearchAsync(new AuditFilter(), new PageQuery(1, 10));

        result.Items.Count.ShouldBeGreaterThanOrEqualTo(2);
        result.Items[0].RecordId.ShouldBe("second");
        result.Items[1].RecordId.ShouldBe("first");
    }

    [Fact]
    public async Task Paging_returns_total()
    {
        var service = CreateService();

        await using (var ctx = Db.CreateContext())
        {
            for (var i = 1; i <= 15; i++)
            {
                ctx.AuditLogs.Add(new AuditLog
                {
                    CreatedDate = new DateTime(2026, 10, 17, 1, 0, 0, DateTimeKind.Utc).AddMinutes(i),
                    Module = AuditModules.Customers,
                    Action = AuditActions.Create,
                    EntityName = "Customer",
                    RecordId = i.ToString(CultureInfo.InvariantCulture),
                    Result = AuditResult.Success
                });
            }
            await ctx.SaveChangesAsync();
        }

        var page1 = await service.SearchAsync(new AuditFilter(), new PageQuery(1, 10));
        page1.Total.ShouldBe(15);
        page1.Items.Count.ShouldBe(10);

        var page2 = await service.SearchAsync(new AuditFilter(), new PageQuery(2, 10));
        page2.Total.ShouldBe(15);
        page2.Items.Count.ShouldBe(5);
    }

    [Fact]
    public async Task Detail_parses_changes_into_property_rows()
    {
        var service = CreateService();
        long logId;

        await using (var ctx = Db.CreateContext())
        {
            var log = new AuditLog
            {
                CreatedDate = new DateTime(2026, 10, 17, 10, 0, 0, DateTimeKind.Utc),
                Module = AuditModules.Customers,
                Action = AuditActions.Update,
                EntityName = "Customer",
                RecordId = "42",
                Result = AuditResult.Success,
                OldValue = "{\"name\":\"Acme Corp\",\"email\":\"old@acme.com\"}",
                NewValue = "{\"name\":\"Acme Inc\",\"email\":\"new@acme.com\"}",
                CorrelationId = "corr-xyz"
            };
            ctx.AuditLogs.Add(log);
            await ctx.SaveChangesAsync();
            logId = log.AuditLogId;
        }

        var detailResult = await service.GetAsync(logId);
        detailResult.Succeeded.ShouldBeTrue();

        var detail = detailResult.Value!;
        detail.CorrelationId.ShouldBe("corr-xyz");
        detail.Changes.Count.ShouldBe(2);

        var nameChange = detail.Changes.First(c => c.Property == "name");
        nameChange.Old.ShouldBe("Acme Corp");
        nameChange.New.ShouldBe("Acme Inc");

        var emailChange = detail.Changes.First(c => c.Property == "email");
        emailChange.Old.ShouldBe("old@acme.com");
        emailChange.New.ShouldBe("new@acme.com");
    }

    [Fact]
    public async Task Text_filter_matches_details_and_username()
    {
        var service = CreateService();

        await using (var ctx = Db.CreateContext())
        {
            ctx.AuditLogs.AddRange(
                new AuditLog
                {
                    CreatedDate = new DateTime(2026, 10, 17, 10, 0, 0, DateTimeKind.Utc),
                    UserName = "special.agent",
                    Details = "Normal message",
                    Module = AuditModules.Customers,
                    Action = AuditActions.Create,
                    EntityName = "Customer",
                    Result = AuditResult.Success
                },
                new AuditLog
                {
                    CreatedDate = new DateTime(2026, 10, 17, 10, 5, 0, DateTimeKind.Utc),
                    UserName = "regular.user",
                    Details = "Contains secretkeyword inside text",
                    Module = AuditModules.Customers,
                    Action = AuditActions.Create,
                    EntityName = "Customer",
                    Result = AuditResult.Success
                }
            );
            await ctx.SaveChangesAsync();
        }

        var match1 = await service.SearchAsync(new AuditFilter(Text: "special.agent"), new PageQuery(1, 10));
        match1.Items.Count.ShouldBe(1);
        match1.Items[0].UserName.ShouldBe("special.agent");

        var match2 = await service.SearchAsync(new AuditFilter(Text: "secretkeyword"), new PageQuery(1, 10));
        match2.Items.Count.ShouldBe(1);
        match2.Items[0].Details.ShouldNotBeNull();
        match2.Items[0].Details!.ShouldContain("secretkeyword");
    }

    [Fact]
    public async Task NonAdmin_Forbidden()
    {
        var service = CreateService(role: "SalesExecutive", userId: "sales-1");

        await Should.ThrowAsync<UnauthorizedAccessException>(() =>
            service.SearchAsync(new AuditFilter(), new PageQuery(1, 10)));

        var detail = await service.GetAsync(1);
        detail.Succeeded.ShouldBeFalse();
        detail.Kind.ShouldBe(ErrorKind.Forbidden);
    }
}

using AcxiomCrm.Application.FollowUps;
using AcxiomCrm.Application.Reporting;

namespace AcxiomCrm.Application.Dashboard;

public sealed record KpiCard(
    string Key,
    string Label,
    decimal Value,
    bool IsMoney,
    int? DeltaInPeriod,
    string? Hint);

public sealed record LabelValue(string Label, decimal Value);

public sealed record StageSeries(string Stage, int Count, decimal Amount, decimal Weighted);

public sealed record MonthPoint(DateOnly Month, decimal Amount, int Count);

public sealed record OwnerPipeline(string OwnerId, string OwnerName, int Count, decimal Amount, decimal Weighted);

public sealed record AdminPanel(int ActiveUsers, int InactiveUsers, int LockedOutUsers, int FailedLoginsLast24h, int SecurityEventsLast7d);

public sealed record FollowUpPanel(int Overdue, int DueToday, int Next7Days, IReadOnlyList<FollowUpListItem> Next5);

public sealed record RolePanels(AdminPanel? Admin, IReadOnlyList<OwnerPipeline>? TeamPipeline, FollowUpPanel? MyFollowUps);

public sealed record DashboardData(
    DateRange Range,
    IReadOnlyList<KpiCard> Cards,
    IReadOnlyList<LabelValue> LeadStatus,
    IReadOnlyList<StageSeries> Pipeline,
    IReadOnlyList<MonthPoint> MonthlySales,
    decimal WeightedPipeline,
    RolePanels Panels);

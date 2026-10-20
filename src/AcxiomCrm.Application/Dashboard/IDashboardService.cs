using System.Diagnostics.CodeAnalysis;
using AcxiomCrm.Application.Common;
using AcxiomCrm.Application.Reporting;

namespace AcxiomCrm.Application.Dashboard;

public interface IDashboardService
{
    [SuppressMessage("Naming", "CA1716:Identifiers should not match keywords", Justification = "Contract defined in plan")]
    Task<Result<DashboardData>> GetAsync(DatePreset preset, DateOnly? from, DateOnly? to, CancellationToken ct = default);
}

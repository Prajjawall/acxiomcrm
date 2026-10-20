using AcxiomCrm.Application.Common;
using AcxiomCrm.Application.Reporting;

namespace AcxiomCrm.Application.Reports;

public interface IReportService
{
    Task<Result<PipelineReportDto>> GetPipelineAsync(ReportFilter filter, CancellationToken ct = default);
    Task<PagedResult<CustomerReportItem>> RunCustomerReportAsync(CustomerReportFilter filter, PageQuery pageQuery, CancellationToken ct = default);
    Task<PagedResult<FollowUpReportItem>> RunFollowUpReportAsync(FollowUpReportFilter filter, PageQuery pageQuery, CancellationToken ct = default);
}

using AcxiomCrm.Domain.Enums;

namespace AcxiomCrm.Web.ViewModels.Opportunities;

public sealed class ChangeStageViewModel
{
    public OpportunityStage TargetStage { get; set; }
    public string? CloseNote { get; set; }
    public string? RowVersion { get; set; }
}

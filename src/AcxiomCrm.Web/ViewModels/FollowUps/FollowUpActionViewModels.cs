using AcxiomCrm.Domain.Validation;
using AcxiomCrm.Web.Validation;

namespace AcxiomCrm.Web.ViewModels.FollowUps;

public sealed class CompleteFollowUpViewModel
{
    public int Id { get; set; }

    [AxMaxLength(Limits.Notes, "Remarks")]
    public string? Remarks { get; set; }

    public bool LogActivity { get; set; } = true;

    public bool ScheduleNext { get; set; }

    public string? RowVersion { get; set; }
}

public sealed class RescheduleFollowUpViewModel
{
    public int Id { get; set; }

    [AxRequired("Follow-up date")]
    [AxFutureDate("Follow-up date")]
    public DateOnly? NewDate { get; set; }

    [AxRequired("Time")]
    public TimeOnly? NewTime { get; set; } = new TimeOnly(10, 0);

    public string? RowVersion { get; set; }
}

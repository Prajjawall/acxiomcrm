using AcxiomCrm.Application.Users;
using AcxiomCrm.Domain.Enums;
using AcxiomCrm.Domain.Validation;
using AcxiomCrm.Web.Validation;

namespace AcxiomCrm.Web.ViewModels.Activities;

public sealed class ActivityFormViewModel
{
    public int Id { get; set; }

    public ActivityType Type { get; set; } = ActivityType.Call;

    [AxRequired("Subject")]
    [AxMaxLength(Limits.Subject, "Subject")]
    public string Subject { get; set; } = string.Empty;

    [AxMaxLength(Limits.Notes, "Description")]
    public string? Description { get; set; }

    [AxRequired("Activity Date")]
    public DateOnly? ActivityDate { get; set; }

    [AxRequired("Time")]
    public TimeOnly? ActivityTime { get; set; } = new TimeOnly(10, 0);

    public int? CustomerId { get; set; }
    public string? CustomerName { get; set; }

    public int? LeadId { get; set; }
    public string? LeadName { get; set; }

    public int? OpportunityId { get; set; }
    public string? OpportunityName { get; set; }

    public string? AssignedToId { get; set; }

    public ActivityStatus Status { get; set; } = ActivityStatus.Completed;

    public string? RowVersion { get; set; }

    public IReadOnlyList<UserOption> AssigneeOptions { get; set; } = [];
}

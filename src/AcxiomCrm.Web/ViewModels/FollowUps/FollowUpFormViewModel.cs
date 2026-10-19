using System.ComponentModel.DataAnnotations;
using AcxiomCrm.Application.Users;
using AcxiomCrm.Domain.Enums;
using AcxiomCrm.Domain.Validation;
using AcxiomCrm.Web.Validation;

namespace AcxiomCrm.Web.ViewModels.FollowUps;

public sealed class FollowUpFormViewModel
{
    public int Id { get; set; }

    [AxRequired("Related Type")]
    public RelatedType RelatedType { get; set; } = RelatedType.Customer;

    [AxRequired("Related record")]
    [Range(1, int.MaxValue, ErrorMessage = "Choose an active record.")]
    public int RelatedId { get; set; }

    public string? RelatedName { get; set; }

    [AxRequired("Follow-up date")]
    [AxFutureDate("Follow-up date")]
    public DateOnly? FollowUpDate { get; set; }

    [AxRequired("Time")]
    public TimeOnly? FollowUpTime { get; set; } = new TimeOnly(10, 0);

    [AxRequired("Subject")]
    [AxMaxLength(Limits.Subject, "Subject")]
    public string Subject { get; set; } = string.Empty;

    public FollowUpType Type { get; set; } = FollowUpType.Call;

    [AxMaxLength(Limits.Notes, "Remarks")]
    public string? Remarks { get; set; }

    public string? AssignedToId { get; set; }

    public bool IsHistoricalEntry { get; set; }

    public string? RowVersion { get; set; }

    public IReadOnlyList<UserOption> AssigneeOptions { get; set; } = [];
}

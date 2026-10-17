using AcxiomCrm.Domain.Enums;

namespace AcxiomCrm.Web.ViewModels.Leads;

public sealed class ChangeStatusViewModel
{
    public LeadStatus To { get; set; }
    public byte[]? RowVersion { get; set; }
}

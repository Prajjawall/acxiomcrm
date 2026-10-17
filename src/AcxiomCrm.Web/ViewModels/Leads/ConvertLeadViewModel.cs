using System.ComponentModel.DataAnnotations;
using AcxiomCrm.Application.Leads;

namespace AcxiomCrm.Web.ViewModels.Leads;

public sealed class ConvertLeadViewModel
{
    public int LeadId { get; set; }
    public string LeadCode { get; set; } = string.Empty;
    public string LeadName { get; set; } = string.Empty;
    public string? LeadCompanyName { get; set; }
    public string? LeadEmail { get; set; }
    public string? LeadPhone { get; set; }
    public decimal ExpectedValue { get; set; }

    public int? ExistingCustomerId { get; set; }
    public string? ExistingCustomerLabel { get; set; }

    public int? SuggestedCustomerId { get; set; }
    public string? SuggestedCustomerLabel { get; set; }

    public bool CreateOpportunity { get; set; }

    public string? OpportunityName { get; set; }

    [Range(0, 1000000000)]
    public decimal OpportunityAmount { get; set; }

    [Range(0, 100)]
    public byte OpportunityProbability { get; set; } = 20;

    public DateOnly? OpportunityExpectedCloseDate { get; set; }

    public string? OpportunityNotes { get; set; }

    public byte[]? RowVersion { get; set; }

    public bool IsOpportunityAvailable { get; set; }

    public ConvertLeadCommand ToCommand(byte[] rowVersion)
    {
        ConvertOpportunityInput? oppInput = null;
        if (CreateOpportunity && !string.IsNullOrWhiteSpace(OpportunityName) && OpportunityExpectedCloseDate.HasValue)
        {
            oppInput = new ConvertOpportunityInput(
                OpportunityName,
                OpportunityAmount,
                OpportunityProbability,
                OpportunityExpectedCloseDate.Value,
                OpportunityNotes);
        }

        return new ConvertLeadCommand(
            ExistingCustomerId,
            CreateOpportunity,
            oppInput,
            rowVersion);
    }
}

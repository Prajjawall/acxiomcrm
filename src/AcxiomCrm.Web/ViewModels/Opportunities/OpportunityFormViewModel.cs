using System.ComponentModel.DataAnnotations;
using AcxiomCrm.Application.Users;
using AcxiomCrm.Domain.Enums;
using AcxiomCrm.Domain.Validation;
using AcxiomCrm.Web.ModelBinding;
using AcxiomCrm.Web.Validation;
using Microsoft.AspNetCore.Mvc;

namespace AcxiomCrm.Web.ViewModels.Opportunities;

public sealed class OpportunityFormViewModel
{
    public int Id { get; set; }

    [AxRequired("Opportunity name")]
    [AxMaxLength(Limits.Name, "Opportunity name")]
    public string Name { get; set; } = string.Empty;

    [AxRequired("Customer")]
    [Range(1, int.MaxValue, ErrorMessage = "Choose an active customer.")]
    public int CustomerId { get; set; }

    public string? CustomerName { get; set; }

    [ModelBinder(typeof(IndianDecimalModelBinder))]
    [Range(0.01, 1000000000.00, ErrorMessage = "Opportunity Amount must be greater than 0.")]
    public decimal Amount { get; set; }

    [AxRange(0, 100, "Opportunity Probability")]
    public int Probability { get; set; } = 20;

    [AxRequired("Expected Close Date")]
    [AxFutureDate("Expected Close Date")]
    public DateOnly? ExpectedCloseDate { get; set; }

    public OpportunityStage Stage { get; set; } = OpportunityStage.Qualification;

    public LeadSource? Source { get; set; }

    public string? OwnerId { get; set; }

    [AxMaxLength(Limits.Notes, "Notes")]
    public string? Notes { get; set; }

    public string? RowVersion { get; set; }

    public IReadOnlyList<UserOption> OwnerOptions { get; set; } = [];
}

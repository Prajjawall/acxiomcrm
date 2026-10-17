using AcxiomCrm.Domain.Enums;
using AcxiomCrm.Domain.Rules;
using AcxiomCrm.Domain.Security;
using Shouldly;

namespace AcxiomCrm.Domain.Tests;

public class LeadWorkflowTests
{
    [Theory]
    // From New
    [InlineData(LeadStatus.New, LeadStatus.New, false)]
    [InlineData(LeadStatus.New, LeadStatus.Contacted, true)]
    [InlineData(LeadStatus.New, LeadStatus.Qualified, false)]
    [InlineData(LeadStatus.New, LeadStatus.Unqualified, true)]
    [InlineData(LeadStatus.New, LeadStatus.Converted, false)]
    [InlineData(LeadStatus.New, LeadStatus.Lost, true)]

    // From Contacted
    [InlineData(LeadStatus.Contacted, LeadStatus.New, false)]
    [InlineData(LeadStatus.Contacted, LeadStatus.Contacted, false)]
    [InlineData(LeadStatus.Contacted, LeadStatus.Qualified, true)]
    [InlineData(LeadStatus.Contacted, LeadStatus.Unqualified, true)]
    [InlineData(LeadStatus.Contacted, LeadStatus.Converted, false)]
    [InlineData(LeadStatus.Contacted, LeadStatus.Lost, true)]

    // From Qualified
    [InlineData(LeadStatus.Qualified, LeadStatus.New, false)]
    [InlineData(LeadStatus.Qualified, LeadStatus.Contacted, true)]
    [InlineData(LeadStatus.Qualified, LeadStatus.Qualified, false)]
    [InlineData(LeadStatus.Qualified, LeadStatus.Unqualified, true)]
    [InlineData(LeadStatus.Qualified, LeadStatus.Converted, true)]
    [InlineData(LeadStatus.Qualified, LeadStatus.Lost, true)]

    // From Unqualified
    [InlineData(LeadStatus.Unqualified, LeadStatus.New, false)]
    [InlineData(LeadStatus.Unqualified, LeadStatus.Contacted, true)]
    [InlineData(LeadStatus.Unqualified, LeadStatus.Qualified, false)]
    [InlineData(LeadStatus.Unqualified, LeadStatus.Unqualified, false)]
    [InlineData(LeadStatus.Unqualified, LeadStatus.Converted, false)]
    [InlineData(LeadStatus.Unqualified, LeadStatus.Lost, true)]

    // From Converted
    [InlineData(LeadStatus.Converted, LeadStatus.New, false)]
    [InlineData(LeadStatus.Converted, LeadStatus.Contacted, false)]
    [InlineData(LeadStatus.Converted, LeadStatus.Qualified, false)]
    [InlineData(LeadStatus.Converted, LeadStatus.Unqualified, false)]
    [InlineData(LeadStatus.Converted, LeadStatus.Converted, false)]
    [InlineData(LeadStatus.Converted, LeadStatus.Lost, false)]

    // From Lost
    [InlineData(LeadStatus.Lost, LeadStatus.New, true)]
    [InlineData(LeadStatus.Lost, LeadStatus.Contacted, false)]
    [InlineData(LeadStatus.Lost, LeadStatus.Qualified, false)]
    [InlineData(LeadStatus.Lost, LeadStatus.Unqualified, false)]
    [InlineData(LeadStatus.Lost, LeadStatus.Converted, false)]
    [InlineData(LeadStatus.Lost, LeadStatus.Lost, false)]
    public void CanTransition_matches_PRD_workflow_matrix(LeadStatus from, LeadStatus to, bool expected)
    {
        LeadWorkflow.CanTransition(from, to).ShouldBe(expected);
    }

    [Fact]
    public void Lost_to_New_allowed_only_for_manager_and_admin()
    {
        LeadWorkflow.AllowedFrom(LeadStatus.Lost, Roles.Admin).ShouldContain(LeadStatus.New);
        LeadWorkflow.AllowedFrom(LeadStatus.Lost, Roles.Manager).ShouldContain(LeadStatus.New);
        LeadWorkflow.AllowedFrom(LeadStatus.Lost, Roles.SalesExecutive).ShouldNotContain(LeadStatus.New);
    }

    [Theory]
    [InlineData(LeadStatus.New)]
    [InlineData(LeadStatus.Contacted)]
    [InlineData(LeadStatus.Qualified)]
    [InlineData(LeadStatus.Unqualified)]
    [InlineData(LeadStatus.Converted)]
    [InlineData(LeadStatus.Lost)]
    public void AllowedFrom_never_contains_Converted(LeadStatus status)
    {
        LeadWorkflow.AllowedFrom(status, Roles.Admin).ShouldNotContain(LeadStatus.Converted);
        LeadWorkflow.AllowedFrom(status, Roles.Manager).ShouldNotContain(LeadStatus.Converted);
        LeadWorkflow.AllowedFrom(status, Roles.SalesExecutive).ShouldNotContain(LeadStatus.Converted);
    }

    [Fact]
    public void Converted_is_terminal()
    {
        LeadWorkflow.AllowedFrom(LeadStatus.Converted, Roles.Admin).ShouldBeEmpty();
        LeadWorkflow.AllowedFrom(LeadStatus.Converted, Roles.Manager).ShouldBeEmpty();
        LeadWorkflow.AllowedFrom(LeadStatus.Converted, Roles.SalesExecutive).ShouldBeEmpty();
    }
}

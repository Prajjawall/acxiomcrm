using AcxiomCrm.Domain.Enums;
using AcxiomCrm.Domain.Security;

namespace AcxiomCrm.Domain.Rules;

public static class LeadWorkflow
{
    public static bool CanTransition(LeadStatus from, LeadStatus to)
    {
        if (from == to)
        {
            return false;
        }

        return from switch
        {
            LeadStatus.New => to is LeadStatus.Contacted or LeadStatus.Unqualified or LeadStatus.Lost,
            LeadStatus.Contacted => to is LeadStatus.Qualified or LeadStatus.Unqualified or LeadStatus.Lost,
            LeadStatus.Qualified => to is LeadStatus.Contacted or LeadStatus.Unqualified or LeadStatus.Converted or LeadStatus.Lost,
            LeadStatus.Unqualified => to is LeadStatus.Contacted or LeadStatus.Lost,
            LeadStatus.Converted => false,
            LeadStatus.Lost => to == LeadStatus.New,
            _ => false
        };
    }

    public static IReadOnlyList<LeadStatus> AllowedFrom(LeadStatus from, string? role)
    {
        return from switch
        {
            LeadStatus.New => [LeadStatus.Contacted, LeadStatus.Unqualified, LeadStatus.Lost],
            LeadStatus.Contacted => [LeadStatus.Qualified, LeadStatus.Unqualified, LeadStatus.Lost],
            LeadStatus.Qualified => [LeadStatus.Contacted, LeadStatus.Unqualified, LeadStatus.Lost],
            LeadStatus.Unqualified => [LeadStatus.Contacted, LeadStatus.Lost],
            LeadStatus.Converted => [],
            LeadStatus.Lost => role is Roles.Admin or Roles.Manager ? [LeadStatus.New] : [],
            _ => []
        };
    }
}

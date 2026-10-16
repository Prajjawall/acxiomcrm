using AcxiomCrm.Domain.Enums;
using AcxiomCrm.Web.Presentation;
using Shouldly;

namespace AcxiomCrm.Web.Tests;

public class StatusBadgeTests
{
    [Fact]
    public void StatusBadges_maps_LeadStatus_correctly()
    {
        var newBadge = StatusBadges.For("Lead", LeadStatus.New);
        newBadge.CssClass.ShouldBe("ax-badge-teal");
        newBadge.Label.ShouldBe("New");

        var qualifiedBadge = StatusBadges.For("Lead", LeadStatus.Qualified);
        qualifiedBadge.CssClass.ShouldBe("ax-badge-success");
        qualifiedBadge.Label.ShouldBe("Qualified");

        var lostBadge = StatusBadges.For("Lead", LeadStatus.Lost);
        lostBadge.CssClass.ShouldBe("ax-badge-danger");
        lostBadge.Label.ShouldBe("Lost");
    }

    [Fact]
    public void StatusBadges_maps_null_to_slate()
    {
        var badge = StatusBadges.For("Customer", null);
        badge.CssClass.ShouldBe("ax-badge-slate");
        badge.Label.ShouldBe("Unknown");
    }
}

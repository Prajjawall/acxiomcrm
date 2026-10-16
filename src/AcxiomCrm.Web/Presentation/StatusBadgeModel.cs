namespace AcxiomCrm.Web.Presentation;

public sealed record StatusBadgeModel(string Label, string CssClass);

public static class StatusBadges
{
    public static StatusBadgeModel For(string domain, object? value)
    {
        if (value == null)
        {
            return new StatusBadgeModel("Unknown", "ax-badge-slate");
        }

        var text = value.ToString() ?? string.Empty;
        var label = Labels.For(value);

        var cssClass = text.ToUpperInvariant() switch
        {
            "ACTIVE" or "QUALIFIED" or "WON" or "COMPLETED" or "CONVERTED" => "ax-badge-success",
            "INACTIVE" or "LOST" or "OVERDUE" or "CANCELLED" or "MISSED" => "ax-badge-danger",
            "CONTACTED" or "INPROGRESS" or "NEGOTIATION" => "ax-badge-info",
            "UNQUALIFIED" or "PROPOSAL" or "PENDING" or "ONHOLD" => "ax-badge-warning",
            "NEW" or "DISCOVERY" or "SCHEDULED" => "ax-badge-teal",
            _ => "ax-badge-slate"
        };

        return new StatusBadgeModel(label, cssClass);
    }
}

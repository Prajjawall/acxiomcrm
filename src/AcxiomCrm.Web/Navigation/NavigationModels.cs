namespace AcxiomCrm.Web.Navigation;

public sealed record NavItem(
    string Label,
    string Icon,
    string Controller,
    string Action,
    string Permission,
    int? Badge = null,
    bool Enabled = true);

public sealed record NavGroup(
    string Title,
    IReadOnlyList<NavItem> Items);

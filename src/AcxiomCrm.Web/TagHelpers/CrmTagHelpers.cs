using System.Text.Encodings.Web;
using AcxiomCrm.Application.Abstractions;
using AcxiomCrm.Web.Presentation;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace AcxiomCrm.Web.TagHelpers;

[HtmlTargetElement("ax-money")]
public sealed class MoneyTagHelper : TagHelper
{
    public decimal Value { get; set; }
    public bool Compact { get; set; }

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        output.TagName = "span";
        output.Attributes.Add("class", "ax-num");
        output.Content.SetContent(Compact ? Formatters.MoneyCompact(Value) : Formatters.Money(Value));
    }
}

[HtmlTargetElement("ax-date")]
public sealed class DateLocalTagHelper : TagHelper
{
    public DateOnly? Value { get; set; }

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        output.TagName = "span";
        if (Value.HasValue)
        {
            output.Content.SetContent(Formatters.Date(Value.Value));
        }
        else
        {
            output.Content.SetContent("—");
        }
    }
}

[HtmlTargetElement("ax-datetime")]
public sealed class DateTimeLocalTagHelper(IClock clock) : TagHelper
{
    public DateTime? Value { get; set; }

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        output.TagName = "span";
        if (Value.HasValue)
        {
            output.Content.SetContent(Formatters.DateTime(Value.Value, clock));
        }
        else
        {
            output.Content.SetContent("—");
        }
    }
}

[HtmlTargetElement("th", Attributes = "ax-sort")]
public sealed class SortHeaderTagHelper : TagHelper
{
    [HtmlAttributeName("ax-sort")]
    public string SortField { get; set; } = string.Empty;

    [HtmlAttributeName("ax-current-sort")]
    public string? CurrentSort { get; set; }

    [HtmlAttributeName("ax-desc")]
    public bool IsDesc { get; set; }

    [ViewContext]
    [HtmlAttributeNotBound]
    public ViewContext ViewContext { get; set; } = null!;

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        var isCurrent = string.Equals(SortField, CurrentSort, StringComparison.OrdinalIgnoreCase);
        var nextDesc = isCurrent && !IsDesc;

        var ariaSort = !isCurrent ? "none" : (IsDesc ? "descending" : "ascending");
        output.Attributes.SetAttribute("aria-sort", ariaSort);

        var indicator = !isCurrent ? "" : (IsDesc ? " ▼" : " ▲");

        var query = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        foreach (var (k, v) in ViewContext.HttpContext.Request.Query)
        {
            query[k] = v.ToString();
        }
        query["sort"] = SortField;
        query["desc"] = nextDesc ? "true" : "false";
        query["page"] = "1";

        var queryString = string.Join("&", query.Where(q => !string.IsNullOrEmpty(q.Value)).Select(q => $"{UrlEncoder.Default.Encode(q.Key)}={UrlEncoder.Default.Encode(q.Value!)}"));
        var url = $"{ViewContext.HttpContext.Request.Path}?{queryString}";

        output.Content.SetHtmlContent($"<a href=\"{url}\" class=\"text-decoration-none text-reset\">{output.GetChildContentAsync().Result.GetContent()}{indicator}</a>");
    }
}

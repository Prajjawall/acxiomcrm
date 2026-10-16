using AcxiomCrm.Web.Presentation;
using Shouldly;

namespace AcxiomCrm.Web.Tests;

public class PagerTests
{
    [Fact]
    public void Calculates_total_pages_correctly()
    {
        var pager = new PagerModel(1, 10, 95, "Index");
        pager.TotalPages.ShouldBe(10);
        pager.FirstItem.ShouldBe(1);
        pager.LastItem.ShouldBe(10);
    }

    [Fact]
    public void Calculates_middle_page_items_correctly()
    {
        var pager = new PagerModel(3, 20, 112, "Index");
        pager.FirstItem.ShouldBe(41);
        pager.LastItem.ShouldBe(60);
    }

    [Fact]
    public void Calculates_last_page_partial_items_correctly()
    {
        var pager = new PagerModel(6, 20, 112, "Index");
        pager.FirstItem.ShouldBe(101);
        pager.LastItem.ShouldBe(112);
    }

    [Fact]
    public void Page_window_contains_first_last_and_surrounding_pages()
    {
        var pager = new PagerModel(5, 10, 100, "Index");
        var window = pager.PageWindow;

        window.ShouldContain(1);
        window.ShouldContain(3);
        window.ShouldContain(4);
        window.ShouldContain(5);
        window.ShouldContain(6);
        window.ShouldContain(7);
        window.ShouldContain(10);
    }
}

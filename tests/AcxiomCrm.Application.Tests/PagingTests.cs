using AcxiomCrm.Application.Common;
using Shouldly;

namespace AcxiomCrm.Application.Tests;

public class PagingTests
{
    [Theory]
    [InlineData(0, 10_000, 1, 100, 0, 100)]
    [InlineData(3, 25, 3, 25, 50, 25)]
    [InlineData(-5, 0, 1, 1, 0, 1)]
    public void PageQuery_clamps(int inputPage, int inputPageSize, int expectedPage, int expectedPageSize, int expectedSkip, int expectedTake)
    {
        var query = new PageQuery(inputPage, inputPageSize);

        query.Page.ShouldBe(expectedPage);
        query.PageSize.ShouldBe(expectedPageSize);
        query.Skip.ShouldBe(expectedSkip);
        query.Take.ShouldBe(expectedTake);
    }
}

using BloodLink.BuildingBlocks.Domain;
using Shouldly;
using Xunit;

namespace BloodLink.BuildingBlocks.UnitTests;

public class PaginationTests
{
    [Theory]
    [InlineData(0, 1)]
    [InlineData(-5, 1)]
    [InlineData(1, 1)]
    [InlineData(42, 42)]
    public void PaginationParams_ValidatedPage_ShouldAlwaysBeAtLeastOne(
        int inputPage,
        int expectedPage
    )
    {
        var parameters = new PaginationParams(Page: inputPage);
        parameters.ValidatedPage.ShouldBe(expectedPage);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(-10, 1)]
    [InlineData(20, 20)]
    [InlineData(100, 100)]
    [InlineData(500, 100)]
    public void PaginationParams_ValidatedPageSize_ShouldBeClampedToMax100(
        int inputPageSize,
        int expectedPageSize
    )
    {
        var parameters = new PaginationParams(PageSize: inputPageSize);
        parameters.ValidatedPageSize.ShouldBe(expectedPageSize);
    }

    [Fact]
    public void PaginationParams_SkipAndTake_ShouldComputeCorrectly()
    {
        var parameters = new PaginationParams(Page: 3, PageSize: 25);
        parameters.Skip.ShouldBe(50);
        parameters.Take.ShouldBe(25);
    }

    [Fact]
    public void PagedResult_TotalPages_AndNavigationFlags_ShouldComputeCorrectly()
    {
        var result = new PagedResult<string>(["a", "b"], Page: 2, PageSize: 2, TotalCount: 5);

        result.TotalPages.ShouldBe(3);
        result.HasPreviousPage.ShouldBeTrue();
        result.HasNextPage.ShouldBeTrue();
    }

    [Fact]
    public void PagedResult_FirstPage_ShouldNotHavePreviousPage()
    {
        var result = new PagedResult<string>(["a", "b"], Page: 1, PageSize: 10, TotalCount: 2);

        result.TotalPages.ShouldBe(1);
        result.HasPreviousPage.ShouldBeFalse();
        result.HasNextPage.ShouldBeFalse();
    }
}

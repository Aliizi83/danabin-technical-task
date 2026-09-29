using Danatadbir.Application.Common;

namespace Danatadbir.Tests.Alerting;

public class PagingHelperTests
{
    private static readonly int[] Ten = Enumerable.Range(1, 10).ToArray();

    [Theory]
    [InlineData(1, 3, new[] { 1, 2, 3 })]
    [InlineData(2, 3, new[] { 4, 5, 6 })]
    [InlineData(4, 3, new[] { 10 })]
    [InlineData(5, 3, new int[0])]
    [InlineData(1, 10, new[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 })]
    [InlineData(1, 1000, new[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 })]
    [InlineData(2, 10, new int[0])]
    public void Slice_returns_the_requested_page(int page, int pageSize, int[] expected) =>
        Assert.Equal(expected, Paging.Slice(Ten, page, pageSize));

    [Theory]
    [InlineData(int.MaxValue, 1000)]
    [InlineData(int.MaxValue, 1)]
    [InlineData(int.MaxValue / 2, 1000)]
    [InlineData(2_147_484, 1000)]     // (page - 1) * pageSize just past int.MaxValue
    public void A_page_number_large_enough_to_overflow_an_int_offset_is_empty(int page, int pageSize) =>
        Assert.Empty(Paging.Slice(Ten, page, pageSize));

    [Fact]
    public void An_empty_list_has_no_pages() => Assert.Empty(Paging.Slice(Array.Empty<int>(), 1, 10));

    [Theory]
    [InlineData(1, 1, null)]
    [InlineData(1, 1000, null)]
    [InlineData(0, 10, "page")]
    [InlineData(-3, 10, "page")]
    [InlineData(1, 0, "pageSize")]
    [InlineData(1, -1, "pageSize")]
    [InlineData(1, 1001, "pageSize")]
    public void Validate_accepts_only_a_positive_page_and_a_bounded_size(int page, int pageSize, string? mentions)
    {
        var error = Paging.Validate(page, pageSize);

        if (mentions is null) Assert.Null(error);
        else Assert.Contains(mentions, error);
    }

    [Theory]
    [InlineData(0, 10, 0)]
    [InlineData(1, 10, 1)]
    [InlineData(10, 10, 1)]
    [InlineData(11, 10, 2)]
    [InlineData(100, 7, 15)]
    public void Total_pages_rounds_up(int total, int pageSize, int pages) =>
        Assert.Equal(pages, Paging.Meta(1, pageSize, total).TotalPages);
}

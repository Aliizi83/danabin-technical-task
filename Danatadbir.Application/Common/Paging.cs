using Danatadbir.Application.Common.Result;

namespace Danatadbir.Application.Common;

public static class Paging
{
    public const int MaxPageSize = 1000;

    /// <summary>Returns a message when the paging parameters are out of range, otherwise null.</summary>
    public static string? Validate(int page, int pageSize) =>
        page < 1 ? "page must be at least 1."
        : pageSize is < 1 or > MaxPageSize ? $"pageSize must be between 1 and {MaxPageSize}."
        : null;

    /// <summary>
    /// The items of one page. The offset is computed in 64 bits: <c>(page - 1) * pageSize</c> in
    /// <c>int</c> overflows for a large page number, turns negative, and a negative <c>Skip</c> is
    /// treated as zero, which would answer a page far past the end with the first page.
    /// </summary>
    public static List<T> Slice<T>(IReadOnlyList<T> items, int page, int pageSize)
    {
        var offset = (long)(page - 1) * pageSize;

        return offset >= items.Count ? [] : items.Skip((int)offset).Take(pageSize).ToList();
    }

    public static PaginationMetaData Meta(int page, int pageSize, int totalCount) =>
        new() { PageNumber = page, PageSize = pageSize, TotalCount = totalCount };
}

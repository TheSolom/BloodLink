using BloodLink.BuildingBlocks.Domain;
using Microsoft.EntityFrameworkCore;

namespace BloodLink.BuildingBlocks.Web;

public static class QueryablePaginationExtensions
{
    public static async Task<PagedResult<T>> ToPagedResultAsync<T>(
        this IQueryable<T> query,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default
    )
    {
        var parameters = new PaginationParams(page, pageSize);
        int totalCount = await query.CountAsync(cancellationToken);
        List<T> items = await query
            .Skip(parameters.Skip)
            .Take(parameters.Take)
            .ToListAsync(cancellationToken);

        return new PagedResult<T>(
            items,
            parameters.ValidatedPage,
            parameters.ValidatedPageSize,
            totalCount
        );
    }

    public static async Task<PagedResult<T>> ToPagedResultAsync<T>(
        this IQueryable<T> query,
        PaginationParams parameters,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(parameters);
        return await query.ToPagedResultAsync(
            parameters.Page,
            parameters.PageSize,
            cancellationToken
        );
    }
}

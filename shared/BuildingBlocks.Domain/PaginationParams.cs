namespace BloodLink.BuildingBlocks.Domain;

public record PaginationParams(int Page = 1, int PageSize = 20)
{
    public const int DefaultPage = 1;
    public const int DefaultPageSize = 20;
    public const int MaxPageSize = 100;

    public int ValidatedPage => Math.Max(1, Page);
    public int ValidatedPageSize => Math.Clamp(PageSize, 1, MaxPageSize);

    public int Skip => (ValidatedPage - 1) * ValidatedPageSize;
    public int Take => ValidatedPageSize;
}

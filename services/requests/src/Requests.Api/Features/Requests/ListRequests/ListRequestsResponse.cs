using BloodLink.BuildingBlocks.Domain;

namespace BloodLink.Requests.Api.Features.Requests.ListRequests;

public record ListRequestsResponse(
    IReadOnlyList<RequestSummaryDto> Items,
    int Page,
    int PageSize,
    int TotalCount
) : PagedResult<RequestSummaryDto>(Items, Page, PageSize, TotalCount);

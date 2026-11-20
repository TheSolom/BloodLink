namespace BloodLink.Requests.Api.Features.Requests.ListRequests;

public record ListRequestsResponse(
    IReadOnlyList<RequestSummaryDto> Items,
    int Page,
    int PageSize,
    int TotalCount
);

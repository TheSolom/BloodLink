namespace BloodLink.Requests.Api.Features.Requests.ListRequests;

public record RequestSummaryDto(
    Guid Id,
    Guid HospitalId,
    string BloodType,
    string Component,
    int UnitsNeeded,
    string Urgency,
    DateTimeOffset NeededBy,
    string Status,
    string IdempotencyKey,
    int AssignedUnitsCount
);

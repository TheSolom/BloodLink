namespace BloodLink.Requests.Api.Features.Requests.SubmitRequest;

public record SubmitRequestResponse(
    Guid Id,
    Guid HospitalId,
    string BloodType,
    string Component,
    int UnitsNeeded,
    string Urgency,
    DateTimeOffset NeededBy,
    string Status,
    string IdempotencyKey,
    IReadOnlyList<Guid> AssignedUnitIds
);

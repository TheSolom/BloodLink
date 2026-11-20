namespace BloodLink.Requests.Api.Features.Requests.SubmitRequest;

public record SubmitRequestCommand(
    string BloodType,
    string Component,
    int UnitsNeeded,
    string Urgency,
    DateTimeOffset NeededBy
);

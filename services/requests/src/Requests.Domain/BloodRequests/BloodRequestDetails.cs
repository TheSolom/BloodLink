using BloodLink.Requests.Domain.ValueObjects;

namespace BloodLink.Requests.Domain.BloodRequests;

public sealed record BloodRequestDetails(
    BloodType BloodType,
    BloodComponent Component,
    int UnitsNeeded,
    RequestUrgency Urgency,
    DateTimeOffset NeededBy
);

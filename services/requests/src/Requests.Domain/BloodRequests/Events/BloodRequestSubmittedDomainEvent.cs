using BloodLink.BuildingBlocks.Domain;
using BloodLink.Requests.Domain.ValueObjects;

namespace BloodLink.Requests.Domain.BloodRequests.Events;

public record BloodRequestSubmittedDomainEvent(
    Guid RequestId,
    Guid HospitalId,
    BloodType BloodType,
    BloodComponent Component,
    int UnitsNeeded,
    RequestUrgency Urgency,
    DateTimeOffset NeededBy,
    string IdempotencyKey
) : DomainEvent;

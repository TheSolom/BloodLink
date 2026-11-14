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

public record BloodRequestMatchingStartedDomainEvent(Guid RequestId) : DomainEvent;

public record BloodRequestReservingStartedDomainEvent(
    Guid RequestId,
    IReadOnlyList<Guid> CandidateUnitIds
) : DomainEvent;

public record BloodRequestDispatchingStartedDomainEvent(Guid RequestId) : DomainEvent;

public record BloodRequestInTransitDomainEvent(Guid RequestId) : DomainEvent;

public record BloodRequestFulfilledDomainEvent(Guid RequestId) : DomainEvent;

public record BloodRequestFailedDomainEvent(Guid RequestId, string Reason) : DomainEvent;

public record BloodRequestCancelledDomainEvent(Guid RequestId, string Reason) : DomainEvent;

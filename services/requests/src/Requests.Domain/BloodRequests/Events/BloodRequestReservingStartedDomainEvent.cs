using BloodLink.BuildingBlocks.Domain;

namespace BloodLink.Requests.Domain.BloodRequests.Events;

public record BloodRequestReservingStartedDomainEvent(
    Guid RequestId,
    IReadOnlyList<Guid> CandidateUnitIds
) : DomainEvent;

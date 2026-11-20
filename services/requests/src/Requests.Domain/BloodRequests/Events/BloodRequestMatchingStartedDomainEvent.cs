using BloodLink.BuildingBlocks.Domain;

namespace BloodLink.Requests.Domain.BloodRequests.Events;

public record BloodRequestMatchingStartedDomainEvent(Guid RequestId) : DomainEvent;

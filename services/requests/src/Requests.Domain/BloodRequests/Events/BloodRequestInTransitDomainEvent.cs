using BloodLink.BuildingBlocks.Domain;

namespace BloodLink.Requests.Domain.BloodRequests.Events;

public record BloodRequestInTransitDomainEvent(Guid RequestId) : DomainEvent;

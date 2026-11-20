using BloodLink.BuildingBlocks.Domain;

namespace BloodLink.Requests.Domain.BloodRequests.Events;

public record BloodRequestDispatchingStartedDomainEvent(Guid RequestId) : DomainEvent;

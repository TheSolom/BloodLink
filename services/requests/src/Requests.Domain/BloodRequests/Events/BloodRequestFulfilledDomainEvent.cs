using BloodLink.BuildingBlocks.Domain;

namespace BloodLink.Requests.Domain.BloodRequests.Events;

public record BloodRequestFulfilledDomainEvent(Guid RequestId) : DomainEvent;

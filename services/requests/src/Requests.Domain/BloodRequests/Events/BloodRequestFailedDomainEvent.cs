using BloodLink.BuildingBlocks.Domain;

namespace BloodLink.Requests.Domain.BloodRequests.Events;

public record BloodRequestFailedDomainEvent(Guid RequestId, string Reason) : DomainEvent;

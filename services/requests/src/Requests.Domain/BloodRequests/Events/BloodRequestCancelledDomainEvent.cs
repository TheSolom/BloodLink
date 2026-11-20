using BloodLink.BuildingBlocks.Domain;

namespace BloodLink.Requests.Domain.BloodRequests.Events;

public record BloodRequestCancelledDomainEvent(Guid RequestId, string Reason) : DomainEvent;

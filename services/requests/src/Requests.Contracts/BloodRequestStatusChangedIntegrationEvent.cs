namespace BloodLink.Requests.Contracts;

public record BloodRequestStatusChangedIntegrationEvent(
    Guid RequestId,
    string Status,
    DateTimeOffset OccurredOn
);

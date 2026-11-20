using MassTransit;

namespace BloodLink.Requests.Api.Infrastructure.Messaging.Consumers;

public class UnitStatusChangedConsumerDefinition : ConsumerDefinition<UnitStatusChangedConsumer>
{
    public UnitStatusChangedConsumerDefinition()
    {
        EndpointName = "requests-unit-status-changed";
        ConcurrentMessageLimit = 1;
    }
}

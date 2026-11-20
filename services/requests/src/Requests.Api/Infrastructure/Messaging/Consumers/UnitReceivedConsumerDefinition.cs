using MassTransit;

namespace BloodLink.Requests.Api.Infrastructure.Messaging.Consumers;

public class UnitReceivedConsumerDefinition : ConsumerDefinition<UnitReceivedConsumer>
{
    public UnitReceivedConsumerDefinition()
    {
        EndpointName = "requests-unit-received";
        ConcurrentMessageLimit = 1;
    }
}

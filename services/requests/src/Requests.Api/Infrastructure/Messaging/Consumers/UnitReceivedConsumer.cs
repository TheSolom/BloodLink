using BloodLink.Inventory.Contracts;
using BloodLink.Requests.Api.Infrastructure.Persistence.ReadModels;
using MassTransit;

namespace BloodLink.Requests.Api.Infrastructure.Messaging.Consumers;

public class UnitReceivedConsumer(IStockReadModelService readModelService)
    : IConsumer<UnitReceivedIntegrationEvent>
{
    public async Task Consume(ConsumeContext<UnitReceivedIntegrationEvent> context)
    {
        await readModelService.ApplyUnitReceivedAsync(context.Message, context.CancellationToken);
    }
}

public class UnitReceivedConsumerDefinition : ConsumerDefinition<UnitReceivedConsumer>
{
    public UnitReceivedConsumerDefinition()
    {
        EndpointName = "requests-unit-received";
        ConcurrentMessageLimit = 1;
    }
}

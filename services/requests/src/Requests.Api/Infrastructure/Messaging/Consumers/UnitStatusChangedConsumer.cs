using BloodLink.Inventory.Contracts;
using BloodLink.Requests.Api.Infrastructure.Persistence.ReadModels;
using MassTransit;

namespace BloodLink.Requests.Api.Infrastructure.Messaging.Consumers;

public class UnitStatusChangedConsumer(IStockReadModelService readModelService)
    : IConsumer<UnitStatusChangedIntegrationEvent>
{
    public async Task Consume(ConsumeContext<UnitStatusChangedIntegrationEvent> context)
    {
        await readModelService.ApplyUnitStatusChangedAsync(
            context.Message,
            context.CancellationToken
        );
    }
}

public class UnitStatusChangedConsumerDefinition : ConsumerDefinition<UnitStatusChangedConsumer>
{
    public UnitStatusChangedConsumerDefinition()
    {
        EndpointName = "requests-unit-status-changed";
        ConcurrentMessageLimit = 1;
    }
}

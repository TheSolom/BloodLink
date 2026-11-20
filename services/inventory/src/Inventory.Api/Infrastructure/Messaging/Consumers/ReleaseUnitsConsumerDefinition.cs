using BloodLink.Inventory.Api.Infrastructure.Persistence;
using MassTransit;

namespace BloodLink.Inventory.Api.Infrastructure.Messaging.Consumers;

public class ReleaseUnitsConsumerDefinition : ConsumerDefinition<ReleaseUnitsConsumer>
{
    protected override void ConfigureConsumer(
        IReceiveEndpointConfigurator endpointConfigurator,
        IConsumerConfigurator<ReleaseUnitsConsumer> consumerConfigurator,
        IRegistrationContext context
    ) => endpointConfigurator.UseEntityFrameworkOutbox<InventoryDbContext>(context);
}

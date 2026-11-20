using BloodLink.Inventory.Api.Infrastructure.Persistence;
using MassTransit;

namespace BloodLink.Inventory.Api.Infrastructure.Messaging.Consumers;

public class ReserveUnitsConsumerDefinition : ConsumerDefinition<ReserveUnitsConsumer>
{
    protected override void ConfigureConsumer(
        IReceiveEndpointConfigurator endpointConfigurator,
        IConsumerConfigurator<ReserveUnitsConsumer> consumerConfigurator,
        IRegistrationContext context
    ) => endpointConfigurator.UseEntityFrameworkOutbox<InventoryDbContext>(context);
}

using BloodLink.Inventory.Api.Features.Reservations.ReserveUnits;
using BloodLink.Inventory.Contracts;
using MassTransit;

namespace BloodLink.Inventory.Api.Infrastructure.Messaging.Consumers;

/// <summary>
/// MassTransit consumer for ReserveUnitsCommand.
/// Sits strictly inside Infrastructure.Messaging and delegates domain processing directly
/// to the vertical slice handler (ReserveUnitsHandler), keeping slice handlers decoupled from MassTransit.
/// Combined with EF Core Inbox deduplication, this consumer is guaranteed to be idempotent.
/// </summary>
public class ReserveUnitsConsumer(ReserveUnitsHandler handler) : IConsumer<ReserveUnitsCommand>
{
    public async Task Consume(ConsumeContext<ReserveUnitsCommand> context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var request = new ReserveUnitsRequest(context.Message.RequestId, context.Message.UnitIds);
        await handler.HandleAsync(request, context.CancellationToken);
    }
}

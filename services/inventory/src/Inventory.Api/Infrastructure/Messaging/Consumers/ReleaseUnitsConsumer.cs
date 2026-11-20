using BloodLink.Inventory.Api.Features.Reservations.ReleaseUnits;
using BloodLink.Inventory.Api.Infrastructure.Persistence;
using BloodLink.Inventory.Contracts;
using MassTransit;

namespace BloodLink.Inventory.Api.Infrastructure.Messaging.Consumers;

/// <summary>
/// MassTransit consumer for ReleaseUnitsCommand.
/// Delegates directly to the ReleaseUnitsHandler vertical slice, guaranteeing idempotent, tolerant release.
/// </summary>
public class ReleaseUnitsConsumer(ReleaseUnitsHandler handler) : IConsumer<ReleaseUnitsCommand>
{
    public async Task Consume(ConsumeContext<ReleaseUnitsCommand> context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var request = new ReleaseUnitsRequest(context.Message.RequestId, context.Message.UnitIds);
        IResult result = await handler.HandleAsync(request, context.CancellationToken);
        if (
            result is IStatusCodeHttpResult
            {
                StatusCode: >= StatusCodes.Status400BadRequest
            } statusResult
        )
        {
            throw new InvalidOperationException(
                $"ReleaseUnitsHandler failed with status code {statusResult.StatusCode} for RequestId {context.Message.RequestId}."
            );
        }
    }
}

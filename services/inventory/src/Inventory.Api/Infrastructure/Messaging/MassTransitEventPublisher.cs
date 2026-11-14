using MassTransit;

namespace BloodLink.Inventory.Api.Infrastructure.Messaging;

/// <summary>
/// MassTransit-backed event publisher. Encapsulates IPublishEndpoint to ensure that
/// higher-level slice handlers and domain operations do not directly reference MassTransit types.
/// When the MassTransit EF Core transactional outbox is enabled, calls to publish are automatically
/// persisted in the database outbox table within the active transaction.
/// </summary>
public class MassTransitEventPublisher(IPublishEndpoint publishEndpoint) : IEventPublisher
{
    public Task PublishAsync<T>(T message, CancellationToken cancellationToken = default)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(message);
        if (typeof(T) == typeof(object))
        {
            return publishEndpoint.Publish(message, message.GetType(), cancellationToken);
        }
        return publishEndpoint.Publish(message, cancellationToken);
    }

    public Task PublishBatchAsync<T>(
        IEnumerable<T> messages,
        CancellationToken cancellationToken = default
    )
        where T : class
    {
        ArgumentNullException.ThrowIfNull(messages);
        if (typeof(T) == typeof(object))
        {
            return Task.WhenAll(
                messages.Select(m => publishEndpoint.Publish(m, m.GetType(), cancellationToken))
            );
        }
        return publishEndpoint.PublishBatch(messages, cancellationToken);
    }
}

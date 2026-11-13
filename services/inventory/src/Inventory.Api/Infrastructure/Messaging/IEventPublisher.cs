namespace BloodLink.Inventory.Api.Infrastructure.Messaging;

/// <summary>
/// Domain and slice abstraction for publishing integration events without direct dependency on messaging frameworks.
/// </summary>
public interface IEventPublisher
{
    Task PublishAsync<T>(T message, CancellationToken cancellationToken = default)
        where T : class;

    Task PublishBatchAsync<T>(
        IEnumerable<T> messages,
        CancellationToken cancellationToken = default
    )
        where T : class;
}

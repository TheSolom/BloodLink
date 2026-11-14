using MassTransit;
using Microsoft.EntityFrameworkCore;

namespace BloodLink.BuildingBlocks.Messaging;

/// <summary>
/// Configures MassTransit Transactional Outbox and Inbox entities within the EF Core model for a specific schema.
/// </summary>
public static class OutboxModelBuilderExtensions
{
    public static void ConfigureTransactionalOutbox(this ModelBuilder modelBuilder, string schema)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        ArgumentException.ThrowIfNullOrWhiteSpace(schema);

        modelBuilder.AddInboxStateEntity(b =>
        {
            b.ToTable("InboxStates", schema);
        });

        modelBuilder.AddOutboxMessageEntity(b =>
        {
            b.ToTable("OutboxMessages", schema);
        });

        modelBuilder.AddOutboxStateEntity(b =>
        {
            b.ToTable("OutboxStates", schema);
        });
    }
}

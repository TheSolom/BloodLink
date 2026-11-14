using MassTransit;
using Microsoft.EntityFrameworkCore;

namespace BloodLink.Inventory.Api.Infrastructure.Messaging;

/// <summary>
/// Configures MassTransit Transactional Outbox and Inbox entities within the EF Core model.
/// Per ADR-0007, keeps direct MassTransit framework dependencies encapsulated within Infrastructure.Messaging.
/// </summary>
public static class OutboxModelBuilderExtensions
{
    public static void ConfigureInventoryOutbox(this ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        modelBuilder.AddInboxStateEntity(b =>
        {
            b.ToTable("InboxStates", "inventory");
        });

        modelBuilder.AddOutboxMessageEntity(b =>
        {
            b.ToTable("OutboxMessages", "inventory");
        });

        modelBuilder.AddOutboxStateEntity(b =>
        {
            b.ToTable("OutboxStates", "inventory");
        });
    }
}

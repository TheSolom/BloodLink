using BloodLink.Inventory.Api.Infrastructure.Messaging.Consumers;
using BloodLink.Inventory.Api.Infrastructure.Persistence;
using MassTransit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace BloodLink.Inventory.Api.Infrastructure.Messaging;

/// <summary>
/// Registers and configures MassTransit messaging, RabbitMQ transport, and EF Core Transactional Outbox/Inbox
/// for the Inventory service. All messaging configurations are kept in Infrastructure.Messaging per ADR-0007.
/// </summary>
public static class MessagingExtensions
{
    public static IServiceCollection AddInventoryMessaging(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddScoped<IEventPublisher, MassTransitEventPublisher>();
        services.AddScoped<DomainEventOutboxInterceptor>(sp => new DomainEventOutboxInterceptor(
            sp
        ));

        services.AddMassTransit(x =>
        {
            x.SetKebabCaseEndpointNameFormatter();

            x.AddConsumer<ReserveUnitsConsumer>();
            x.AddConsumer<ReleaseUnitsConsumer>();

            x.AddEntityFrameworkOutbox<InventoryDbContext>(o =>
            {
                o.UseSqlServer();
                o.UseBusOutbox();
                o.QueryDelay = TimeSpan.FromSeconds(1);
                o.DuplicateDetectionWindow = TimeSpan.FromMinutes(30);
            });

            x.UsingRabbitMq(
                (context, cfg) =>
                {
                    IConfigurationSection rabbitMqSection = configuration.GetSection("RabbitMq");
                    string host = rabbitMqSection["Host"] ?? "localhost";
                    ushort port = ushort.TryParse(rabbitMqSection["Port"], out var p)
                        ? p
                        : (ushort)5672;
                    string virtualHost = rabbitMqSection["VirtualHost"] ?? "/";
                    string username = rabbitMqSection["UserName"] ?? "guest";
                    string password = rabbitMqSection["Password"] ?? "guest";

                    cfg.Host(
                        host,
                        port,
                        virtualHost,
                        h =>
                        {
                            h.Username(username);
                            h.Password(password);
                        }
                    );

                    // Configure standard message retry policy: 3 retries with exponential backoff
                    cfg.UseMessageRetry(r =>
                        r.Exponential(
                            3,
                            TimeSpan.FromMilliseconds(200),
                            TimeSpan.FromSeconds(2),
                            TimeSpan.FromMilliseconds(200)
                        )
                    );

                    cfg.ConfigureEndpoints(context);
                }
            );
        });

        return services;
    }
}

using BloodLink.BuildingBlocks.Messaging;
using BloodLink.Requests.Api.Infrastructure.Messaging.Consumers;
using BloodLink.Requests.Api.Infrastructure.Persistence;
using MassTransit;

namespace BloodLink.Requests.Api.Infrastructure.Messaging;

public static class RequestsMessagingExtensions
{
    public static IServiceCollection AddRequestsMessaging(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddScoped<IEventPublisher, MassTransitEventPublisher>();

        services.AddMassTransit(x =>
        {
            x.SetKebabCaseEndpointNameFormatter();

            x.AddConsumer<UnitReceivedConsumer, UnitReceivedConsumerDefinition>();
            x.AddConsumer<UnitStatusChangedConsumer, UnitStatusChangedConsumerDefinition>();

            x.AddEntityFrameworkOutbox<RequestsDbContext>(o =>
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
                    ushort port = ushort.TryParse(rabbitMqSection["Port"], out ushort p)
                        ? p
                        : (ushort)5672;
                    string virtualHost = rabbitMqSection["VirtualHost"] ?? "/";
                    string username = rabbitMqSection["UserName"] ?? "guest";
                    string password = rabbitMqSection["Password"] ?? "guest";

                    cfg.Host(host, port, virtualHost, h =>
                    {
                        h.Username(username);
                        h.Password(password);
                    });

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

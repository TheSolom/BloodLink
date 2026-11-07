using Microsoft.Extensions.Options;

namespace BloodLink.Inventory.Api.Features.Expiry.ExpireUnits;

public class ExpireUnitsBackgroundService(
    IServiceScopeFactory scopeFactory,
    TimeProvider timeProvider,
    IOptions<ExpireUnitsOptions> options,
    ILogger<ExpireUnitsBackgroundService> logger
) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Value.Enabled)
        {
            if (logger.IsEnabled(LogLevel.Information))
            {
                logger.LogInformation("ExpireUnitsBackgroundService is disabled by configuration");
            }
            return;
        }

        if (logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation(
                "ExpireUnitsBackgroundService started with sweep interval {Interval}",
                options.Value.SweepInterval
            );
        }

        using var timer = new PeriodicTimer(options.Value.SweepInterval, timeProvider);

        while (
            !stoppingToken.IsCancellationRequested
            && await timer.WaitForNextTickAsync(stoppingToken)
        )
        {
            try
            {
                await using AsyncServiceScope scope = scopeFactory.CreateAsyncScope();
                ExpireUnitsHandler handler =
                    scope.ServiceProvider.GetRequiredService<ExpireUnitsHandler>();
                await handler.ExecuteSweepAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                if (logger.IsEnabled(LogLevel.Error))
                {
                    logger.LogError(
                        ex,
                        "Unhandled error occurred during expiration sweep execution"
                    );
                }
            }
        }
    }
}

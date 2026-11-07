using BloodLink.Inventory.Api.Infrastructure.Persistence;
using BloodLink.Inventory.Domain.Model.BloodUnits;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace BloodLink.Inventory.Api.Features.Expiry.ExpireUnits;

public class ExpireUnitsHandler(
    InventoryDbContext dbContext,
    TimeProvider timeProvider,
    IOptions<ExpireUnitsOptions> options,
    ILogger<ExpireUnitsHandler> logger
)
{
    public async Task<int> ExecuteSweepAsync(CancellationToken cancellationToken)
    {
        DateTimeOffset now = timeProvider.GetUtcNow();
        int batchSize = options.Value.BatchSize;

        // Fetch candidate units: Available or Reserved where ExpiresAt <= now
        List<BloodUnit> expiredCandidates = await dbContext
            .BloodUnits.Where(u =>
                (u.Status == BloodUnitStatus.Available || u.Status == BloodUnitStatus.Reserved)
                && u.ExpiresAt <= now
            )
            .Take(batchSize)
            .ToListAsync(cancellationToken);

        if (expiredCandidates.Count == 0)
        {
            return 0;
        }

        int expiredCount = 0;
        foreach (BloodUnit? unit in expiredCandidates)
        {
            try
            {
                unit.Expire(now);
                expiredCount++;
            }
            catch (Exception ex)
            {
                if (logger.IsEnabled(LogLevel.Warning))
                {
                    logger.LogWarning(ex, "Failed to transition unit {UnitId} to Expired", unit.Id);
                }
            }
        }

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            if (logger.IsEnabled(LogLevel.Warning))
            {
                logger.LogWarning(ex, "Concurrency conflict during expiration sweep");
            }

            return 0;
        }

        if (logger.IsEnabled(LogLevel.Information) && expiredCount > 0)
        {
            logger.LogInformation(
                "Expiration sweep completed: expired {Count} units",
                expiredCount
            );
        }

        return expiredCount;
    }
}

using BloodLink.Inventory.Api.Infrastructure.Messaging;
using BloodLink.Inventory.Api.Infrastructure.Persistence;
using BloodLink.Inventory.Contracts;
using BloodLink.Inventory.Domain.Model.BloodUnits;
using Microsoft.EntityFrameworkCore;

namespace BloodLink.Inventory.Api.Features.Reservations.ReleaseUnits;

public class ReleaseUnitsHandler(
    InventoryDbContext dbContext,
    IEventPublisher eventPublisher,
    ILogger<ReleaseUnitsHandler> logger
)
{
    public async Task<IResult> HandleAsync(
        ReleaseUnitsRequest request,
        CancellationToken cancellationToken
    )
    {
        // 1. Query requested units
        List<BloodUnit> units = await dbContext
            .BloodUnits.Where(u => request.UnitIds.Contains(u.Id))
            .ToListAsync(cancellationToken);

        // 2. Validate existence: if any unit ID is not found, return 404
        if (units.Count != request.UnitIds.Count)
        {
            var foundIds = units.Select(u => u.Id).ToHashSet();
            var missingIds = request.UnitIds.Where(id => !foundIds.Contains(id)).ToList();

            if (logger.IsEnabled(LogLevel.Warning))
            {
                logger.LogWarning(
                    "Release rejected for RequestId {RequestId}. Missing unit IDs count: {Count}",
                    request.RequestId,
                    missingIds.Count
                );
            }

            return Results.Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: "Blood Units Not Found",
                detail: $"One or more blood units were not found in inventory: {string.Join(", ", missingIds)}",
                extensions: new Dictionary<string, object?>
                {
                    ["code"] = "stock.unit_not_found",
                    ["missingUnitIds"] = missingIds,
                }
            );
        }

        // 3. Tolerant release: find units currently reserved by this RequestId
        var unitsToRelease = units
            .Where(u =>
                u.Status == BloodUnitStatus.Reserved
                && u.CurrentReservationRequestId == request.RequestId
            )
            .ToList();

        foreach (BloodUnit? unit in unitsToRelease)
        {
            unit.ReleaseReservation(request.RequestId);
        }

        // 4. Save changes if any units were released
        if (unitsToRelease.Count > 0)
        {
            var releasedIds = unitsToRelease.Select(u => u.Id).ToList();
            await eventPublisher.PublishAsync(
                new UnitsReleasedIntegrationEvent(request.RequestId, releasedIds),
                cancellationToken
            );

            try
            {
                await dbContext.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateConcurrencyException ex)
            {
                if (logger.IsEnabled(LogLevel.Warning))
                {
                    logger.LogWarning(
                        ex,
                        "Concurrency conflict during release for RequestId {RequestId}",
                        request.RequestId
                    );
                }

                return Results.Problem(
                    statusCode: StatusCodes.Status409Conflict,
                    title: "Release Concurrency Conflict",
                    detail: "One or more blood units were modified concurrently by another transaction.",
                    extensions: new Dictionary<string, object?>
                    {
                        ["code"] = "release.concurrency_conflict",
                    }
                );
            }
        }

        if (logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation(
                "Successfully released {Count} units for RequestId {RequestId}",
                unitsToRelease.Count,
                request.RequestId
            );
        }

        var releasedResultIds = unitsToRelease.Select(u => u.Id).ToList();
        return Results.Ok(new ReleaseUnitsResponse(request.RequestId, releasedResultIds));
    }
}

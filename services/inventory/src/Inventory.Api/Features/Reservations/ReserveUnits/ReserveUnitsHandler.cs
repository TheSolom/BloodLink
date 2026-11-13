using BloodLink.Inventory.Api.Infrastructure.Messaging;
using BloodLink.Inventory.Api.Infrastructure.Persistence;
using BloodLink.Inventory.Contracts;
using BloodLink.Inventory.Domain.Model.BloodUnits;
using Microsoft.EntityFrameworkCore;

namespace BloodLink.Inventory.Api.Features.Reservations.ReserveUnits;

public class ReserveUnitsHandler(
    InventoryDbContext dbContext,
    TimeProvider timeProvider,
    IEventPublisher eventPublisher,
    ILogger<ReserveUnitsHandler> logger
)
{
    public virtual async Task<IResult> HandleAsync(
        ReserveUnitsRequest request,
        CancellationToken cancellationToken
    )
    {
        DateTimeOffset now = timeProvider.GetUtcNow();

        // 1. Load requested units
        List<BloodUnit> units = await dbContext
            .BloodUnits.Where(u => request.UnitIds.Contains(u.Id))
            .ToListAsync(cancellationToken);

        // 2. Validate existence
        if (units.Count != request.UnitIds.Count)
        {
            var foundIds = units.Select(u => u.Id).ToHashSet();
            var missingIds = request.UnitIds.Where(id => !foundIds.Contains(id)).ToList();

            if (logger.IsEnabled(LogLevel.Warning))
            {
                logger.LogWarning(
                    "Reservation rejected for RequestId {RequestId}. Missing unit IDs count: {Count}",
                    request.RequestId,
                    missingIds.Count
                );
            }

            await eventPublisher.PublishAsync(
                new ReservationRejectedIntegrationEvent(
                    request.RequestId,
                    missingIds,
                    $"One or more blood units were not found in inventory: {string.Join(", ", missingIds)}"
                ),
                cancellationToken
            );
            await dbContext.SaveChangesAsync(cancellationToken);

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

        // 3. Idempotency check: if already reserved by this RequestId, return 200 OK
        if (
            units.All(u =>
                u.Status == BloodUnitStatus.Reserved
                && u.CurrentReservationRequestId == request.RequestId
            )
        )
        {
            if (logger.IsEnabled(LogLevel.Information))
            {
                logger.LogInformation(
                    "Idempotent reservation retry received for RequestId {RequestId}",
                    request.RequestId
                );
            }

            return Results.Ok(new ReserveUnitsResponse(request.RequestId, request.UnitIds));
        }

        // 4. Availability check (all-or-nothing: every unit must be Available and not expired)
        var unavailableUnits = units
            .Where(u => u.Status != BloodUnitStatus.Available || now >= u.ExpiresAt)
            .Select(u => u.Id)
            .ToList();

        if (unavailableUnits.Count > 0)
        {
            if (logger.IsEnabled(LogLevel.Warning))
            {
                logger.LogWarning(
                    "Reservation rejected for RequestId {RequestId}. Unavailable units count: {Count}",
                    request.RequestId,
                    unavailableUnits.Count
                );
            }

            await eventPublisher.PublishAsync(
                new ReservationRejectedIntegrationEvent(
                    request.RequestId,
                    unavailableUnits,
                    "One or more requested blood units are no longer available for reservation."
                ),
                cancellationToken
            );
            await dbContext.SaveChangesAsync(cancellationToken);

            return Results.Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "Blood Units Unavailable",
                detail: "One or more requested blood units are no longer available for reservation.",
                extensions: new Dictionary<string, object?>
                {
                    ["code"] = "reservation.unavailable",
                    ["unavailableUnitIds"] = unavailableUnits,
                }
            );
        }

        // 5. Reserve each unit (increments Version, sets Status & CurrentReservationRequestId)
        foreach (BloodUnit? unit in units)
        {
            unit.Reserve(request.RequestId, now);
        }

        await eventPublisher.PublishAsync(
            new UnitsReservedIntegrationEvent(request.RequestId, request.UnitIds),
            cancellationToken
        );

        // 6. Save changes in one transaction, catching concurrency conflicts
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            dbContext.ChangeTracker.Clear();
            List<BloodUnit> current = await dbContext
                .BloodUnits.AsNoTracking()
                .Where(u => request.UnitIds.Contains(u.Id))
                .ToListAsync(cancellationToken);
            if (
                current.Count == request.UnitIds.Count
                && current.All(u =>
                    u.Status == BloodUnitStatus.Reserved
                    && u.CurrentReservationRequestId == request.RequestId
                )
            )
            {
                return Results.Ok(new ReserveUnitsResponse(request.RequestId, request.UnitIds));
            }

            if (logger.IsEnabled(LogLevel.Warning))
            {
                logger.LogWarning(
                    ex,
                    "Concurrency conflict during reservation for RequestId {RequestId}",
                    request.RequestId
                );
            }

            await eventPublisher.PublishAsync(
                new ReservationRejectedIntegrationEvent(
                    request.RequestId,
                    request.UnitIds,
                    "One or more blood units were modified concurrently by another transaction."
                ),
                cancellationToken
            );
            await dbContext.SaveChangesAsync(cancellationToken);

            return Results.Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "Reservation Concurrency Conflict",
                detail: "One or more blood units were modified concurrently by another transaction.",
                extensions: new Dictionary<string, object?>
                {
                    ["code"] = "reservation.concurrency_conflict",
                }
            );
        }

        if (logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation(
                "Successfully reserved {Count} units for RequestId {RequestId}",
                units.Count,
                request.RequestId
            );
        }

        return Results.Ok(new ReserveUnitsResponse(request.RequestId, request.UnitIds));
    }
}

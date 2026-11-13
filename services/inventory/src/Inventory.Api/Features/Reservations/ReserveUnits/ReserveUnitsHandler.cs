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
    ILogger<ReserveUnitsHandler> logger,
    IServiceScopeFactory scopeFactory
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
            return await HandleMissingUnitsAsync(request, units, cancellationToken);
        }

        // 3. Idempotency check: if already reserved by this RequestId, return 200 OK
        if (IsAlreadyReserved(units, request.RequestId))
        {
            LogIdempotentRetry(request.RequestId);
            return Results.Ok(new ReserveUnitsResponse(request.RequestId, request.UnitIds));
        }

        // 4. Availability check (all-or-nothing: every unit must be Available and not expired)
        List<Guid> unavailableUnits = GetUnavailableUnits(units, now);
        if (unavailableUnits.Count > 0)
        {
            return await HandleUnavailableUnitsAsync(request, unavailableUnits, cancellationToken);
        }

        // 5. Reserve each unit (increments Version, sets Status & CurrentReservationRequestId)
        foreach (BloodUnit unit in units)
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
            return await HandleConcurrencyConflictAsync(request, ex, cancellationToken);
        }

        LogSuccess(units.Count, request.RequestId);
        return Results.Ok(new ReserveUnitsResponse(request.RequestId, request.UnitIds));
    }

    private static bool IsAlreadyReserved(List<BloodUnit> units, Guid requestId) =>
        units.All(u =>
            u.Status == BloodUnitStatus.Reserved && u.CurrentReservationRequestId == requestId
        );

    private static List<Guid> GetUnavailableUnits(List<BloodUnit> units, DateTimeOffset now) =>
        units
            .Where(u => u.Status != BloodUnitStatus.Available || now >= u.ExpiresAt)
            .Select(u => u.Id)
            .ToList();

    private async Task<IResult> HandleMissingUnitsAsync(
        ReserveUnitsRequest request,
        List<BloodUnit> units,
        CancellationToken cancellationToken
    )
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

    private async Task<IResult> HandleUnavailableUnitsAsync(
        ReserveUnitsRequest request,
        List<Guid> unavailableUnits,
        CancellationToken cancellationToken
    )
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

    private async Task<IResult> HandleConcurrencyConflictAsync(
        ReserveUnitsRequest request,
        DbUpdateConcurrencyException ex,
        CancellationToken cancellationToken
    )
    {
        if (logger.IsEnabled(LogLevel.Warning))
        {
            logger.LogWarning(
                ex,
                "Concurrency conflict during reservation for RequestId {RequestId}",
                request.RequestId
            );
        }

        try
        {
            using IServiceScope scope = scopeFactory.CreateScope();
            InventoryDbContext checkDbContext =
                scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
            List<BloodUnit> current = await checkDbContext
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

            IEventPublisher scopePublisher =
                scope.ServiceProvider.GetRequiredService<IEventPublisher>();
            await scopePublisher.PublishAsync(
                new ReservationRejectedIntegrationEvent(
                    request.RequestId,
                    request.UnitIds,
                    "One or more blood units were modified concurrently by another transaction."
                ),
                cancellationToken
            );
            await checkDbContext.SaveChangesAsync(cancellationToken);
        }
        catch (Exception outboxEx)
        {
            logger.LogWarning(
                outboxEx,
                "Failed to record rejection event in outbox for RequestId {RequestId}",
                request.RequestId
            );
        }

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

    private void LogIdempotentRetry(Guid requestId)
    {
        if (logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation(
                "Idempotent reservation retry received for RequestId {RequestId}",
                requestId
            );
        }
    }

    private void LogSuccess(int count, Guid requestId)
    {
        if (logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation(
                "Successfully reserved {Count} units for RequestId {RequestId}",
                count,
                requestId
            );
        }
    }
}

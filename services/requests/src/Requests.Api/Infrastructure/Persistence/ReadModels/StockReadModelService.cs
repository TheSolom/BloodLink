using BloodLink.Inventory.Contracts;
using BloodLink.Requests.Api.Infrastructure.Clients;
using BloodLink.Requests.Domain.Matching;
using BloodLink.Requests.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace BloodLink.Requests.Api.Infrastructure.Persistence.ReadModels;

public interface IStockReadModelService
{
    Task ApplyUnitReceivedAsync(
        UnitReceivedIntegrationEvent unitReceived,
        CancellationToken ct = default
    );
    Task ApplyUnitStatusChangedAsync(
        UnitStatusChangedIntegrationEvent unitStatusChanged,
        CancellationToken ct = default
    );
    Task MergeSnapshotItemAsync(StockSnapshotUnitDto snapshot, CancellationToken ct = default);
    Task<IReadOnlyList<CandidateUnitSnapshot>> GetAvailableCandidateUnitsAsync(
        CancellationToken ct = default
    );
}

public class StockReadModelService(RequestsDbContext dbContext) : IStockReadModelService
{
    public async Task ApplyUnitReceivedAsync(
        UnitReceivedIntegrationEvent unitReceived,
        CancellationToken ct = default
    )
    {
        ArgumentNullException.ThrowIfNull(unitReceived);

        StockItemReadModel? existing = await dbContext.StockReadModels.FirstOrDefaultAsync(
            u => u.UnitId == unitReceived.UnitId,
            ct
        );

        if (existing == null)
        {
            var newEntry = new StockItemReadModel
            {
                UnitId = unitReceived.UnitId,
                BloodBankId = unitReceived.BloodBankId,
                BloodType = unitReceived.BloodType,
                Component = unitReceived.Component,
                ExpiresAt = unitReceived.ExpiresAt,
                Status = "Available",
                Version = unitReceived.Version,
            };

            var bankCoords = await dbContext
                .StockReadModels.Where(u =>
                    u.BloodBankId == unitReceived.BloodBankId
                    && u.Latitude.HasValue
                    && u.Longitude.HasValue
                )
                .Select(u => new { u.Latitude, u.Longitude })
                .FirstOrDefaultAsync(ct);

            if (bankCoords != null)
            {
                newEntry.Latitude = bankCoords.Latitude;
                newEntry.Longitude = bankCoords.Longitude;
            }

            CheckAndSetComplete(newEntry);
            dbContext.StockReadModels.Add(newEntry);
            await dbContext.SaveChangesAsync(ct);
            return;
        }

        // Apply rules: UnitReceived upserts attributes if incoming Version >= stored Version
        if (unitReceived.Version >= existing.Version)
        {
            existing.BloodBankId = unitReceived.BloodBankId;
            existing.BloodType = unitReceived.BloodType;
            existing.Component = unitReceived.Component;
            existing.ExpiresAt = unitReceived.ExpiresAt;
            existing.Version = unitReceived.Version;

            if (string.IsNullOrEmpty(existing.Status))
            {
                existing.Status = "Available";
            }

            if (!existing.Latitude.HasValue || !existing.Longitude.HasValue)
            {
                var bankCoords = await dbContext
                    .StockReadModels.Where(u =>
                        u.BloodBankId == unitReceived.BloodBankId
                        && u.Latitude.HasValue
                        && u.Longitude.HasValue
                    )
                    .Select(u => new { u.Latitude, u.Longitude })
                    .FirstOrDefaultAsync(ct);

                if (bankCoords != null)
                {
                    existing.Latitude = bankCoords.Latitude;
                    existing.Longitude = bankCoords.Longitude;
                }
            }

            CheckAndSetComplete(existing);
            await dbContext.SaveChangesAsync(ct);
        }
        // If incoming Version < stored Version: ignore stale event
    }

    public async Task ApplyUnitStatusChangedAsync(
        UnitStatusChangedIntegrationEvent unitStatusChanged,
        CancellationToken ct = default
    )
    {
        ArgumentNullException.ThrowIfNull(unitStatusChanged);

        StockItemReadModel? existing = await dbContext.StockReadModels.FirstOrDefaultAsync(
            u => u.UnitId == unitStatusChanged.UnitId,
            ct
        );

        if (existing == null)
        {
            // Park incomplete row holding Status and Version
            var parked = new StockItemReadModel
            {
                UnitId = unitStatusChanged.UnitId,
                Status = unitStatusChanged.Status,
                Version = unitStatusChanged.Version,
                IsComplete = false,
            };

            dbContext.StockReadModels.Add(parked);
            await dbContext.SaveChangesAsync(ct);
            return;
        }

        // UnitStatusChanged updates Status only if incoming Version > stored Version
        if (unitStatusChanged.Version > existing.Version)
        {
            existing.Status = unitStatusChanged.Status;
            existing.Version = unitStatusChanged.Version;
            CheckAndSetComplete(existing);
            await dbContext.SaveChangesAsync(ct);
        }
        // Duplicates and older versions are ignored
    }

    public async Task MergeSnapshotItemAsync(
        StockSnapshotUnitDto snapshot,
        CancellationToken ct = default
    )
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        StockItemReadModel? existing = await dbContext.StockReadModels.FirstOrDefaultAsync(
            u => u.UnitId == snapshot.UnitId,
            ct
        );

        if (existing == null)
        {
            var newEntry = new StockItemReadModel
            {
                UnitId = snapshot.UnitId,
                BloodBankId = snapshot.BloodBankId,
                Latitude = snapshot.Latitude,
                Longitude = snapshot.Longitude,
                BloodType = snapshot.BloodType,
                Component = snapshot.Component,
                ExpiresAt = snapshot.ExpiresAt,
                Status = snapshot.Status,
                Version = snapshot.Version,
                IsComplete = true,
            };

            dbContext.StockReadModels.Add(newEntry);
            await dbContext.SaveChangesAsync(ct);
            return;
        }

        // Snapshot rows never overwrite newer event data
        if (snapshot.Version >= existing.Version)
        {
            existing.BloodBankId = snapshot.BloodBankId;
            existing.Latitude = snapshot.Latitude;
            existing.Longitude = snapshot.Longitude;
            existing.BloodType = snapshot.BloodType;
            existing.Component = snapshot.Component;
            existing.ExpiresAt = snapshot.ExpiresAt;
            existing.Status = snapshot.Status;
            existing.Version = snapshot.Version;
            existing.IsComplete = true;
        }
        else
        {
            // Snapshot has older version, but existing might be an incomplete parked row needing attributes
            if (!existing.IsComplete)
            {
                existing.BloodBankId ??= snapshot.BloodBankId;
                existing.Latitude ??= snapshot.Latitude;
                existing.Longitude ??= snapshot.Longitude;
                existing.BloodType ??= snapshot.BloodType;
                existing.Component ??= snapshot.Component;
                existing.ExpiresAt ??= snapshot.ExpiresAt;
                CheckAndSetComplete(existing);
            }
        }

        await dbContext.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<CandidateUnitSnapshot>> GetAvailableCandidateUnitsAsync(
        CancellationToken ct = default
    )
    {
        List<StockItemReadModel> candidates = await dbContext
            .StockReadModels.AsNoTracking()
            .Where(u => u.IsComplete && u.Status == "Available")
            .ToListAsync(ct);

        var list = new List<CandidateUnitSnapshot>();
        foreach (StockItemReadModel c in candidates)
        {
            if (
                c.BloodBankId.HasValue
                && c.ExpiresAt.HasValue
                && BloodType.TryParse(c.BloodType, out BloodType? bloodType)
                && Enum.TryParse<BloodComponent>(
                    c.Component,
                    ignoreCase: true,
                    out BloodComponent component
                )
            )
            {
                list.Add(
                    new CandidateUnitSnapshot(
                        c.UnitId,
                        c.BloodBankId.Value,
                        new GeoLocation(c.Latitude ?? 0.0, c.Longitude ?? 0.0),
                        bloodType,
                        component,
                        c.ExpiresAt.Value,
                        c.Version
                    )
                );
            }
        }

        return list;
    }

    private static void CheckAndSetComplete(StockItemReadModel item)
    {
        item.IsComplete =
            item.BloodBankId.HasValue
            && item.ExpiresAt.HasValue
            && !string.IsNullOrEmpty(item.BloodType)
            && !string.IsNullOrEmpty(item.Component)
            && !string.IsNullOrEmpty(item.Status);
    }
}

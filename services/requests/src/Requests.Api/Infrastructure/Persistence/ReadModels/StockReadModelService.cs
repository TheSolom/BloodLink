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
            await InsertNewStockItemAsync(unitReceived, ct);
            return;
        }

        await UpdateExistingStockItemAsync(existing, unitReceived, ct);
    }

    private async Task InsertNewStockItemAsync(
        UnitReceivedIntegrationEvent unitReceived,
        CancellationToken ct
    )
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

        await PopulateCoordinatesIfMissingAsync(newEntry, unitReceived.BloodBankId, ct);
        CheckAndSetComplete(newEntry);
        dbContext.StockReadModels.Add(newEntry);
        await dbContext.SaveChangesAsync(ct);
    }

    private async Task UpdateExistingStockItemAsync(
        StockItemReadModel existing,
        UnitReceivedIntegrationEvent unitReceived,
        CancellationToken ct
    )
    {
        if (unitReceived.Version >= existing.Version)
        {
            ApplyCurrentVersionUnitReceived(existing, unitReceived);
            await PopulateCoordinatesIfMissingAsync(existing, unitReceived.BloodBankId, ct);
            CheckAndSetComplete(existing);
            await dbContext.SaveChangesAsync(ct);
        }
        else if (!existing.IsComplete)
        {
            HydrateParkedRowAttributes(existing, unitReceived);
            await PopulateCoordinatesIfMissingAsync(existing, unitReceived.BloodBankId, ct);
            CheckAndSetComplete(existing);
            await dbContext.SaveChangesAsync(ct);
        }
    }

    private static void ApplyCurrentVersionUnitReceived(
        StockItemReadModel existing,
        UnitReceivedIntegrationEvent unitReceived
    )
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
    }

    private static void HydrateParkedRowAttributes(
        StockItemReadModel existing,
        UnitReceivedIntegrationEvent unitReceived
    )
    {
        existing.BloodBankId ??= unitReceived.BloodBankId;
        existing.BloodType ??= unitReceived.BloodType;
        existing.Component ??= unitReceived.Component;
        existing.ExpiresAt ??= unitReceived.ExpiresAt;
    }

    private async Task PopulateCoordinatesIfMissingAsync(
        StockItemReadModel item,
        Guid bankId,
        CancellationToken ct
    )
    {
        if (item.Latitude.HasValue && item.Longitude.HasValue)
        {
            return;
        }

        var bankCoords = await dbContext
            .StockReadModels.Where(u =>
                u.BloodBankId == bankId && u.Latitude.HasValue && u.Longitude.HasValue
            )
            .Select(u => new { u.Latitude, u.Longitude })
            .FirstOrDefaultAsync(ct);

        if (bankCoords != null)
        {
            item.Latitude = bankCoords.Latitude;
            item.Longitude = bankCoords.Longitude;
        }
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
                && c.Latitude.HasValue
                && c.Longitude.HasValue
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
                        new GeoLocation(c.Latitude.Value, c.Longitude.Value),
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
            && item.Latitude.HasValue
            && item.Longitude.HasValue
            && item.ExpiresAt.HasValue
            && !string.IsNullOrEmpty(item.BloodType)
            && !string.IsNullOrEmpty(item.Component)
            && !string.IsNullOrEmpty(item.Status);
    }
}

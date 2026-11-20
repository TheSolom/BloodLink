using BloodLink.Inventory.Api.Infrastructure.Persistence;
using BloodLink.Inventory.Domain.Model.BloodUnits;
using Microsoft.EntityFrameworkCore;

namespace BloodLink.Inventory.Api.Features.Stock.GetStockSnapshot;

public class GetStockSnapshotHandler(InventoryDbContext dbContext)
{
    public async Task<GetStockSnapshotResponse> HandleAsync(
        GetStockSnapshotQuery query,
        CancellationToken cancellationToken
    )
    {
        int limit = Math.Clamp(query.PageSize, 1, 1000);

        var baseQuery =
            from u in dbContext.BloodUnits.AsNoTracking()
            join b in dbContext.BloodBanks.AsNoTracking() on u.BloodBankId equals b.Id
            where
                u.Status == BloodUnitStatus.Available
                || u.Status == BloodUnitStatus.Reserved
                || u.Status == BloodUnitStatus.InTransit
            select new { Unit = u, Bank = b };

        if (query.Cursor.HasValue)
        {
            Guid cursor = query.Cursor.Value;
            baseQuery = baseQuery.Where(x => x.Unit.Id.CompareTo(cursor) > 0);
        }

        List<StockSnapshotUnitDto> results = await baseQuery
            .OrderBy(x => x.Unit.Id)
            .Take(limit + 1)
            .Select(x => new StockSnapshotUnitDto(
                x.Unit.Id,
                x.Unit.BloodBankId,
                x.Bank.Location.Latitude,
                x.Bank.Location.Longitude,
                x.Unit.BloodType.ToString(),
                x.Unit.Component.ToString(),
                x.Unit.ExpiresAt,
                x.Unit.Status.ToString(),
                x.Unit.Version
            ))
            .ToListAsync(cancellationToken);

        bool hasMore = results.Count > limit;
        List<StockSnapshotUnitDto> items = hasMore ? results.Take(limit).ToList() : results;
        Guid? nextCursor = hasMore && items.Count > 0 ? items[^1].UnitId : null;

        return new GetStockSnapshotResponse(items, nextCursor, hasMore);
    }
}

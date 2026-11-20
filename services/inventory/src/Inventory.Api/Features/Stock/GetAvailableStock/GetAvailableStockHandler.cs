using BloodLink.BuildingBlocks.Domain;
using BloodLink.BuildingBlocks.Web;
using BloodLink.Inventory.Api.Infrastructure.Persistence;
using BloodLink.Inventory.Domain.Model.BloodUnits;
using BloodLink.Inventory.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace BloodLink.Inventory.Api.Features.Stock.GetAvailableStock;

public class GetAvailableStockHandler(InventoryDbContext dbContext, TimeProvider timeProvider)
{
    public async Task<GetAvailableStockResponse> HandleAsync(
        GetAvailableStockQuery query,
        CancellationToken cancellationToken
    )
    {
        DateTimeOffset now = timeProvider.GetUtcNow();

        // Baseline: Available status and not yet expired
        IQueryable<BloodUnit> unitsQuery = dbContext
            .BloodUnits.AsNoTracking()
            .Where(u => u.Status == BloodUnitStatus.Available && u.ExpiresAt > now);

        if (
            !string.IsNullOrWhiteSpace(query.BloodType)
            && BloodType.TryParse(query.BloodType, out BloodType? bloodType)
        )
        {
            unitsQuery = unitsQuery.Where(u => u.BloodType == bloodType);
        }

        if (
            !string.IsNullOrWhiteSpace(query.Component)
            && BloodComponentExtensions.TryParse(query.Component, out BloodComponent component)
        )
        {
            unitsQuery = unitsQuery.Where(u => u.Component == component);
        }

        if (query.BankId.HasValue)
        {
            unitsQuery = unitsQuery.Where(u => u.BloodBankId == query.BankId.Value);
        }

        if (query.ExpiringBefore.HasValue)
        {
            unitsQuery = unitsQuery.Where(u => u.ExpiresAt <= query.ExpiringBefore.Value);
        }

        PagedResult<StockItemDto> paged = await unitsQuery
            .OrderBy(u => u.ExpiresAt)
            .ThenBy(u => u.Id)
            .Select(u => new StockItemDto(
                u.Id,
                u.DonationNumber.Value,
                u.BloodType.ToString(),
                u.Component.ToString(),
                u.CollectedAt,
                u.ExpiresAt,
                u.Status.ToString(),
                u.BloodBankId
            ))
            .ToPagedResultAsync(query.Page, query.PageSize, cancellationToken);

        return new GetAvailableStockResponse(
            paged.Items,
            paged.Page,
            paged.PageSize,
            paged.TotalCount
        );
    }
}

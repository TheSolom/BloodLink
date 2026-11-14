using System.Net;
using System.Net.Http.Json;
using BloodLink.Inventory.Api.Features.Stock.GetStockSnapshot;
using BloodLink.Inventory.Api.Infrastructure.Persistence;
using BloodLink.Inventory.Domain.Model.BloodBanks;
using BloodLink.Inventory.Domain.Model.BloodUnits;
using BloodLink.Inventory.Domain.ValueObjects;
using BloodLink.Inventory.IntegrationTests.Fixtures;
using Shouldly;
using Xunit;

namespace BloodLink.Inventory.IntegrationTests;

[Collection("InventoryIntegrationTests")]
public class GetStockSnapshotTests : IAsyncLifetime
{
    private readonly DatabaseFixture _databaseFixture;
    private readonly InventoryApiFactory _factory;
    private readonly HttpClient _client;

    public GetStockSnapshotTests(DatabaseFixture databaseFixture)
    {
        _databaseFixture = databaseFixture;
        _factory = new InventoryApiFactory(databaseFixture);
        _client = _factory.CreateClient();
    }

    public async Task InitializeAsync() => await _databaseFixture.ResetDatabaseAsync();

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _factory.DisposeAsync();
    }

    [Fact]
    public async Task GetStockSnapshot_ShouldReturnNonTerminalUnits_AndExcludeTerminalUnits()
    {
        DateTimeOffset now = _factory.TimeProvider.GetUtcNow();
        Guid bankId = await SeedBloodBankAsync("Central Bank", 51.5074, -0.1278);
        Guid requestId = Guid.NewGuid();

        Guid availId = Guid.NewGuid();
        Guid resId = Guid.NewGuid();
        Guid inTransitId = Guid.NewGuid();
        Guid expId = Guid.NewGuid();
        Guid delivId = Guid.NewGuid();

        await using (InventoryDbContext dbContext = _factory.CreateDbContext())
        {
            // 1. Available (non-terminal -> INCLUDE)
            var uAvail = BloodUnit.ReceiveDonation(
                availId,
                DonationNumber.Create("DN-01"),
                BloodType.Parse("O-"),
                BloodComponent.RedCells,
                now.AddDays(-1),
                bankId,
                now
            );

            // 2. Reserved (non-terminal -> INCLUDE)
            var uRes = BloodUnit.ReceiveDonation(
                resId,
                DonationNumber.Create("DN-02"),
                BloodType.Parse("O+"),
                BloodComponent.RedCells,
                now.AddDays(-1),
                bankId,
                now
            );
            uRes.Reserve(requestId, now);

            // 3. InTransit (non-terminal -> INCLUDE)
            var uTransit = BloodUnit.ReceiveDonation(
                inTransitId,
                DonationNumber.Create("DN-03"),
                BloodType.Parse("A+"),
                BloodComponent.Platelets,
                now.AddDays(-1),
                bankId,
                now
            );
            uTransit.Reserve(requestId, now);
            uTransit.MarkInTransit(requestId);

            // 4. Expired (terminal -> EXCLUDE)
            var uExp = BloodUnit.ReceiveDonation(
                expId,
                DonationNumber.Create("DN-04"),
                BloodType.Parse("B-"),
                BloodComponent.Platelets,
                now.AddDays(-6),
                bankId,
                now.AddDays(-5)
            );
            uExp.Expire(now);

            // 5. Delivered (terminal -> EXCLUDE)
            var uDeliv = BloodUnit.ReceiveDonation(
                delivId,
                DonationNumber.Create("DN-05"),
                BloodType.Parse("AB+"),
                BloodComponent.RedCells,
                now.AddDays(-1),
                bankId,
                now
            );
            uDeliv.Reserve(requestId, now);
            uDeliv.MarkInTransit(requestId);
            uDeliv.MarkDelivered(requestId);

            dbContext.BloodUnits.AddRange(uAvail, uRes, uTransit, uExp, uDeliv);
            await dbContext.SaveChangesAsync();
        }

        HttpResponseMessage response = await _client.GetAsync(
            "/internal/stock-snapshot?pageSize=50"
        );
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        GetStockSnapshotResponse? snapshot =
            await response.Content.ReadFromJsonAsync<GetStockSnapshotResponse>();
        snapshot.ShouldNotBeNull();
        snapshot.Items.Count.ShouldBe(3);

        var returnedIds = snapshot.Items.Select(x => x.UnitId).ToList();
        returnedIds.ShouldContain(availId);
        returnedIds.ShouldContain(resId);
        returnedIds.ShouldContain(inTransitId);
        returnedIds.ShouldNotContain(expId);
        returnedIds.ShouldNotContain(delivId);

        // Verify version and bank location are returned
        var resDto = snapshot.Items.First(x => x.UnitId == resId);
        resDto.Version.ShouldBe(2); // Initial 1 + Reserve 1 = 2
        resDto.Status.ShouldBe(nameof(BloodUnitStatus.Reserved));
        resDto.Latitude.ShouldBe(51.5074);
        resDto.Longitude.ShouldBe(-0.1278);
    }

    [Fact]
    public async Task GetStockSnapshot_CursorPagination_ShouldBeStableAndCompleteAcrossPages()
    {
        DateTimeOffset now = _factory.TimeProvider.GetUtcNow();
        Guid bankId = await SeedBloodBankAsync("Pagination Bank", 52.0, 0.0);
        var createdIds = new List<Guid>();

        await using (InventoryDbContext dbContext = _factory.CreateDbContext())
        {
            for (int i = 0; i < 5; i++)
            {
                Guid id = Guid.NewGuid();
                createdIds.Add(id);
                var unit = BloodUnit.ReceiveDonation(
                    id,
                    DonationNumber.Create($"DN-PAG-{i:D2}"),
                    BloodType.Parse("O+"),
                    BloodComponent.RedCells,
                    now.AddDays(-1),
                    bankId,
                    now
                );
                dbContext.BloodUnits.Add(unit);
            }
            await dbContext.SaveChangesAsync();
        }

        // Fetch page by page with pageSize = 2
        var allFetchedIds = new List<Guid>();
        Guid? nextCursor = null;
        int pageCount = 0;

        do
        {
            pageCount++;
            string url = nextCursor.HasValue
                ? $"/internal/stock-snapshot?pageSize=2&cursor={nextCursor.Value}"
                : "/internal/stock-snapshot?pageSize=2";

            var response = await _client.GetAsync(url);
            response.StatusCode.ShouldBe(HttpStatusCode.OK);

            var page = await response.Content.ReadFromJsonAsync<GetStockSnapshotResponse>();
            page.ShouldNotBeNull();
            allFetchedIds.AddRange(page.Items.Select(x => x.UnitId));
            nextCursor = page.NextCursor;

            if (page.HasMore)
            {
                page.Items.Count.ShouldBe(2);
                nextCursor.ShouldNotBeNull();
            }
        } while (nextCursor.HasValue);

        pageCount.ShouldBe(3); // 2 + 2 + 1 = 5 across 3 pages
        allFetchedIds.Count.ShouldBe(5);
        allFetchedIds.Distinct().Count().ShouldBe(5);
        allFetchedIds.ShouldBe(
            createdIds.OrderBy(id => id).ToList(),
            "Items must be deterministically ordered by UnitId"
        );
    }

    private async Task<Guid> SeedBloodBankAsync(string name, double lat = 51.5, double lon = -0.1)
    {
        await using InventoryDbContext dbContext = _factory.CreateDbContext();
        var bank = BloodBank.Register(Guid.NewGuid(), name, GeoLocation.Create(lat, lon));
        dbContext.BloodBanks.Add(bank);
        await dbContext.SaveChangesAsync();
        return bank.Id;
    }
}

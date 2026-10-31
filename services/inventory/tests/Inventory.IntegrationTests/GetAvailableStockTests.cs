using System.Net;
using System.Net.Http.Json;
using System.Reflection;
using BloodLink.Inventory.Api.Features.Stock.GetAvailableStock;
using BloodLink.Inventory.Domain.Model.BloodBanks;
using BloodLink.Inventory.Domain.Model.BloodUnits;
using BloodLink.Inventory.Domain.ValueObjects;
using BloodLink.Inventory.IntegrationTests.Fixtures;
using Shouldly;
using Xunit;

namespace BloodLink.Inventory.IntegrationTests;

[Collection("InventoryIntegrationTests")]
public class GetAvailableStockTests : IAsyncLifetime
{
    private readonly DatabaseFixture _databaseFixture;
    private readonly InventoryApiFactory _factory;
    private readonly HttpClient _client;

    public GetAvailableStockTests(DatabaseFixture databaseFixture)
    {
        _databaseFixture = databaseFixture;
        _factory = new InventoryApiFactory(databaseFixture);
        _client = _factory.CreateClient();
    }

    public async Task InitializeAsync()
    {
        await _databaseFixture.ResetDatabaseAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Should_ReturnOnlyAvailableAndNonExpiredStock_ExcludingReservedDeliveredAndExpired()
    {
        var now = _factory.TimeProvider.GetUtcNow();
        var bankId = await SeedBloodBankAsync("General Bank");

        await using (var dbContext = _factory.CreateDbContext())
        {
            // Unit 1: Available & valid (should be returned)
            var u1 = BloodUnit.ReceiveDonation(Guid.NewGuid(), DonationNumber.Create("DN-AVAIL-01"), BloodType.Parse("O-"), BloodComponent.RedCells, now.AddDays(-2), bankId, now);

            // Unit 2: Reserved (should NOT be returned)
            var u2 = BloodUnit.ReceiveDonation(Guid.NewGuid(), DonationNumber.Create("DN-RES-02"), BloodType.Parse("O-"), BloodComponent.RedCells, now.AddDays(-2), bankId, now);
            u2.Reserve(Guid.NewGuid(), now);

            // Unit 3: Delivered (should NOT be returned)
            var u3 = BloodUnit.ReceiveDonation(Guid.NewGuid(), DonationNumber.Create("DN-DEL-03"), BloodType.Parse("A+"), BloodComponent.RedCells, now.AddDays(-2), bankId, now);
            var req3 = Guid.NewGuid();
            u3.Reserve(req3, now);
            u3.MarkInTransit(req3);
            u3.MarkDelivered(req3);

            // Unit 4: Explicitly Expired status (should NOT be returned)
            var u4 = BloodUnit.ReceiveDonation(Guid.NewGuid(), DonationNumber.Create("DN-EXP-04"), BloodType.Parse("B+"), BloodComponent.Platelets, now.AddDays(-4), bankId, now);
            // Expire requires now >= ExpiresAt, so check time at +2 days
            u4.Expire(now.AddDays(2));

            // Unit 5: Status == Available, but ExpiresAt <= now (expired-but-still-Available in DB, should NOT be returned)
            var u5 = BloodUnit.ReceiveDonation(Guid.NewGuid(), DonationNumber.Create("DN-PAST-05"), BloodType.Parse("AB+"), BloodComponent.Platelets, now.AddDays(-3), bankId, now);
            // Artificially set ExpiresAt in the past via reflection to simulate a unit whose status hasn't been swept yet
            typeof(BloodUnit).GetProperty(nameof(BloodUnit.ExpiresAt))!
                .SetValue(u5, now.AddSeconds(-10));

            dbContext.BloodUnits.AddRange(u1, u2, u3, u4, u5);
            await dbContext.SaveChangesAsync();
        }

        var response = await _client.GetAsync("/api/v1/stock");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<GetAvailableStockResponse>();
        body.ShouldNotBeNull();
        body.TotalCount.ShouldBe(1);
        body.Items.Count.ShouldBe(1);
        body.Items[0].DonationNumber.ShouldBe("DN-AVAIL-01");
        body.Items[0].BloodType.ShouldBe("O-");
        body.Items[0].Status.ShouldBe("Available");
    }

    [Fact]
    public async Task Should_FilterByBloodTypeAndComponent()
    {
        var now = _factory.TimeProvider.GetUtcNow();
        var bankId = await SeedBloodBankAsync("Filtering Bank");

        await using (var dbContext = _factory.CreateDbContext())
        {
            var u1 = BloodUnit.ReceiveDonation(Guid.NewGuid(), DonationNumber.Create("DN-O-MINUS"), BloodType.Parse("O-"), BloodComponent.RedCells, now.AddDays(-1), bankId, now);
            var u2 = BloodUnit.ReceiveDonation(Guid.NewGuid(), DonationNumber.Create("DN-A-PLUS"), BloodType.Parse("A+"), BloodComponent.RedCells, now.AddDays(-1), bankId, now);
            var u3 = BloodUnit.ReceiveDonation(Guid.NewGuid(), DonationNumber.Create("DN-O-PLT"), BloodType.Parse("O-"), BloodComponent.Platelets, now.AddDays(-1), bankId, now);

            dbContext.BloodUnits.AddRange(u1, u2, u3);
            await dbContext.SaveChangesAsync();
        }

        // Filter by BloodType=O- and Component=RedCells
        var response = await _client.GetAsync("/api/v1/stock?bloodType=O-&component=RedCells");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<GetAvailableStockResponse>();
        body.ShouldNotBeNull();
        body.TotalCount.ShouldBe(1);
        body.Items.Count.ShouldBe(1);
        body.Items[0].DonationNumber.ShouldBe("DN-O-MINUS");
    }

    [Fact]
    public async Task Should_PaginateAndOrderByExpiresAtThenId()
    {
        var now = _factory.TimeProvider.GetUtcNow();
        var bankId = await SeedBloodBankAsync("Pagination Bank");

        await using (var dbContext = _factory.CreateDbContext())
        {
            // Unit A expires in 10 days
            var uA = BloodUnit.ReceiveDonation(Guid.Parse("11111111-1111-1111-1111-111111111111"), DonationNumber.Create("DN-PAGE-01"), BloodType.Parse("O+"), BloodComponent.RedCells, now.AddDays(-32), bankId, now);
            // Unit B expires in 20 days
            var uB = BloodUnit.ReceiveDonation(Guid.Parse("22222222-2222-2222-2222-222222222222"), DonationNumber.Create("DN-PAGE-02"), BloodType.Parse("O+"), BloodComponent.RedCells, now.AddDays(-22), bankId, now);
            // Unit C expires in 30 days
            var uC = BloodUnit.ReceiveDonation(Guid.Parse("33333333-3333-3333-3333-333333333333"), DonationNumber.Create("DN-PAGE-03"), BloodType.Parse("O+"), BloodComponent.RedCells, now.AddDays(-12), bankId, now);

            dbContext.BloodUnits.AddRange(uA, uB, uC);
            await dbContext.SaveChangesAsync();
        }

        // Request page 1 with pageSize 2 (should return uA and uB in order of ExpiresAt)
        var page1Response = await _client.GetAsync("/api/v1/stock?page=1&pageSize=2");
        page1Response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var page1 = await page1Response.Content.ReadFromJsonAsync<GetAvailableStockResponse>();
        page1.ShouldNotBeNull();
        page1.TotalCount.ShouldBe(3);
        page1.Items.Count.ShouldBe(2);
        page1.Items[0].DonationNumber.ShouldBe("DN-PAGE-01");
        page1.Items[1].DonationNumber.ShouldBe("DN-PAGE-02");

        // Request page 2 with pageSize 2 (should return uC)
        var page2Response = await _client.GetAsync("/api/v1/stock?page=2&pageSize=2");
        page2Response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var page2 = await page2Response.Content.ReadFromJsonAsync<GetAvailableStockResponse>();
        page2.ShouldNotBeNull();
        page2.TotalCount.ShouldBe(3);
        page2.Items.Count.ShouldBe(1);
        page2.Items[0].DonationNumber.ShouldBe("DN-PAGE-03");
    }

    private async Task<Guid> SeedBloodBankAsync(string name)
    {
        await using var dbContext = _factory.CreateDbContext();
        var bank = BloodBank.Register(Guid.NewGuid(), name, GeoLocation.Create(40.0, -70.0));
        dbContext.BloodBanks.Add(bank);
        await dbContext.SaveChangesAsync();
        return bank.Id;
    }
}


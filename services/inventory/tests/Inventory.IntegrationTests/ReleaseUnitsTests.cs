using System.Net;
using BloodLink.Inventory.Api.Features.Reservations.ReleaseUnits;
using BloodLink.Inventory.Api.Features.Reservations.ReserveUnits;
using BloodLink.Inventory.Api.Infrastructure.Persistence;
using BloodLink.Inventory.Domain.Model.BloodBanks;
using BloodLink.Inventory.Domain.Model.BloodUnits;
using BloodLink.Inventory.Domain.ValueObjects;
using BloodLink.Inventory.IntegrationTests.Fixtures;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Xunit;

namespace BloodLink.Inventory.IntegrationTests;

[Collection("InventoryIntegrationTests")]
public class ReleaseUnitsTests : IAsyncLifetime
{
    private readonly DatabaseFixture _databaseFixture;
    private readonly InventoryApiFactory _factory;
    private readonly HttpClient _client;

    public ReleaseUnitsTests(DatabaseFixture databaseFixture)
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
    public async Task Should_ReleaseUnits_Successfully_WhenUnitsReservedByRequest()
    {
        Guid bankId = await SeedBloodBankAsync("Release Test Bank");
        DateTimeOffset now = _factory.TimeProvider.GetUtcNow();
        Guid unit1Id = await SeedBloodUnitAsync(bankId, "DN-REL-01", now.AddDays(-2));
        Guid unit2Id = await SeedBloodUnitAsync(bankId, "DN-REL-02", now.AddDays(-2));

        var requestId = Guid.NewGuid();
        // Reserve units first
        HttpResponseMessage reserveResponse = await _client.PostAsJsonAsync(
            "/api/v1/stock/reserve",
            new ReserveUnitsRequest(requestId, [unit1Id, unit2Id])
        );
        reserveResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        // Now release them
        var releaseRequest = new ReleaseUnitsRequest(requestId, [unit1Id, unit2Id]);
        HttpResponseMessage response = await _client.PostAsJsonAsync(
            "/api/v1/stock/release",
            releaseRequest
        );

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        ReleaseUnitsResponse? body =
            await response.Content.ReadFromJsonAsync<ReleaseUnitsResponse>();
        body.ShouldNotBeNull();
        body.RequestId.ShouldBe(requestId);
        body.ReleasedUnitIds.Count.ShouldBe(2);
        body.ReleasedUnitIds.ShouldContain(unit1Id);
        body.ReleasedUnitIds.ShouldContain(unit2Id);

        await using InventoryDbContext dbContext = _factory.CreateDbContext();
        List<BloodUnit> unitsInDb = await dbContext
            .BloodUnits.Where(u => u.Id == unit1Id || u.Id == unit2Id)
            .ToListAsync();
        unitsInDb.Count.ShouldBe(2);
        unitsInDb.ShouldAllBe(u => u.Status == BloodUnitStatus.Available);
        unitsInDb.ShouldAllBe(u => u.CurrentReservationRequestId == null);
        unitsInDb.ShouldAllBe(u => u.Version == 3);
    }

    [Fact]
    public async Task Should_TolerateAndReleaseRemaining_WhenSomeUnitsAlreadyReleasedOrNotReserved()
    {
        Guid bankId = await SeedBloodBankAsync("Release Tolerant Bank");
        DateTimeOffset now = _factory.TimeProvider.GetUtcNow();
        Guid unitReservedId = await SeedBloodUnitAsync(bankId, "DN-REL-TOL-01", now.AddDays(-2));
        Guid unitAvailableId = await SeedBloodUnitAsync(bankId, "DN-REL-TOL-02", now.AddDays(-2));

        var requestId = Guid.NewGuid();
        // Reserve only unitReservedId
        HttpResponseMessage reserveResponse = await _client.PostAsJsonAsync(
            "/api/v1/stock/reserve",
            new ReserveUnitsRequest(requestId, [unitReservedId])
        );
        reserveResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        // Send release request for both units
        var releaseRequest = new ReleaseUnitsRequest(requestId, [unitReservedId, unitAvailableId]);
        HttpResponseMessage response = await _client.PostAsJsonAsync(
            "/api/v1/stock/release",
            releaseRequest
        );

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        ReleaseUnitsResponse? body =
            await response.Content.ReadFromJsonAsync<ReleaseUnitsResponse>();
        body.ShouldNotBeNull();
        body.RequestId.ShouldBe(requestId);
        // Only the unit that was actually reserved by this requestId should be in ReleasedUnitIds
        body.ReleasedUnitIds.Count.ShouldBe(1);
        body.ReleasedUnitIds.ShouldContain(unitReservedId);

        await using InventoryDbContext dbContext = _factory.CreateDbContext();
        BloodUnit? unitReserved = await dbContext.BloodUnits.FindAsync(unitReservedId);
        unitReserved.ShouldNotBeNull();
        unitReserved.Status.ShouldBe(BloodUnitStatus.Available);
        unitReserved.CurrentReservationRequestId.ShouldBeNull();
    }

    [Fact]
    public async Task Should_ReturnNotFound_WhenUnitDoesNotExist()
    {
        var nonExistentId = Guid.NewGuid();
        var requestId = Guid.NewGuid();
        var releaseRequest = new ReleaseUnitsRequest(requestId, [nonExistentId]);

        HttpResponseMessage response = await _client.PostAsJsonAsync(
            "/api/v1/stock/release",
            releaseRequest
        );

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        ProblemDetails? problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        problem.ShouldNotBeNull();
        problem.Extensions["code"]?.ToString().ShouldBe("stock.unit_not_found");
    }

    [Fact]
    public async Task Should_BeIdempotent_WhenRepeatingRelease()
    {
        Guid bankId = await SeedBloodBankAsync("Release Idempotent Bank");
        DateTimeOffset now = _factory.TimeProvider.GetUtcNow();
        Guid unitId = await SeedBloodUnitAsync(bankId, "DN-REL-IDEM", now.AddDays(-2));

        var requestId = Guid.NewGuid();
        await _client.PostAsJsonAsync(
            "/api/v1/stock/reserve",
            new ReserveUnitsRequest(requestId, [unitId])
        );

        // First release
        var releaseRequest = new ReleaseUnitsRequest(requestId, [unitId]);
        HttpResponseMessage firstResponse = await _client.PostAsJsonAsync(
            "/api/v1/stock/release",
            releaseRequest
        );
        firstResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        // Second release: idempotent, returns 200 OK without failing
        HttpResponseMessage secondResponse = await _client.PostAsJsonAsync(
            "/api/v1/stock/release",
            releaseRequest
        );
        secondResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        ReleaseUnitsResponse? body =
            await secondResponse.Content.ReadFromJsonAsync<ReleaseUnitsResponse>();
        body.ShouldNotBeNull();
        body.RequestId.ShouldBe(requestId);
    }

    private async Task<Guid> SeedBloodBankAsync(string name)
    {
        await using InventoryDbContext dbContext = _factory.CreateDbContext();
        var bank = BloodBank.Register(Guid.NewGuid(), name, GeoLocation.Create(40.0, -70.0));
        dbContext.BloodBanks.Add(bank);
        await dbContext.SaveChangesAsync();
        return bank.Id;
    }

    private async Task<Guid> SeedBloodUnitAsync(
        Guid bankId,
        string donationNumber,
        DateTimeOffset collectedAt
    )
    {
        await using InventoryDbContext dbContext = _factory.CreateDbContext();
        DateTimeOffset now = _factory.TimeProvider.GetUtcNow();
        var unit = BloodUnit.ReceiveDonation(
            Guid.NewGuid(),
            DonationNumber.Create(donationNumber),
            BloodType.Parse("O-"),
            BloodComponent.RedCells,
            collectedAt,
            bankId,
            now
        );
        dbContext.BloodUnits.Add(unit);
        await dbContext.SaveChangesAsync();
        return unit.Id;
    }
}

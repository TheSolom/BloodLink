using System.Net;
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
public class ReserveUnitsTests : IAsyncLifetime
{
    private readonly DatabaseFixture _databaseFixture;
    private readonly InventoryApiFactory _factory;
    private readonly HttpClient _client;

    public ReserveUnitsTests(DatabaseFixture databaseFixture)
    {
        _databaseFixture = databaseFixture;
        _factory = new InventoryApiFactory(databaseFixture);
        _client = _factory.CreateClient();
    }

    public async Task InitializeAsync() => await _databaseFixture.ResetDatabaseAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Should_ReserveUnits_Successfully_WhenUnitsAvailable()
    {
        Guid bankId = await SeedBloodBankAsync("Reserve Test Bank");
        DateTimeOffset now = _factory.TimeProvider.GetUtcNow();
        Guid unit1Id = await SeedBloodUnitAsync(bankId, "DN-RES-01", now.AddDays(-2));
        Guid unit2Id = await SeedBloodUnitAsync(bankId, "DN-RES-02", now.AddDays(-2));

        var requestId = Guid.NewGuid();
        var request = new ReserveUnitsRequest(requestId, [unit1Id, unit2Id]);

        HttpResponseMessage response = await _client.PostAsJsonAsync(
            "/api/v1/stock/reserve",
            request
        );

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        ReserveUnitsResponse? body =
            await response.Content.ReadFromJsonAsync<ReserveUnitsResponse>();
        body.ShouldNotBeNull();
        body.RequestId.ShouldBe(requestId);
        body.ReservedUnitIds.Count.ShouldBe(2);
        body.ReservedUnitIds.ShouldContain(unit1Id);
        body.ReservedUnitIds.ShouldContain(unit2Id);

        await using InventoryDbContext dbContext = _factory.CreateDbContext();
        List<BloodUnit> unitsInDb = await dbContext
            .BloodUnits.Where(u => u.Id == unit1Id || u.Id == unit2Id)
            .ToListAsync();
        unitsInDb.Count.ShouldBe(2);
        unitsInDb.ShouldAllBe(u => u.Status == BloodUnitStatus.Reserved);
        unitsInDb.ShouldAllBe(u => u.CurrentReservationRequestId == requestId);
        unitsInDb.ShouldAllBe(u => u.Version == 2);
    }

    [Fact]
    public async Task Should_ReturnNotFound_WhenUnitDoesNotExist()
    {
        var nonExistentUnitId = Guid.NewGuid();
        var requestId = Guid.NewGuid();
        var request = new ReserveUnitsRequest(requestId, [nonExistentUnitId]);

        HttpResponseMessage response = await _client.PostAsJsonAsync(
            "/api/v1/stock/reserve",
            request
        );

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        ProblemDetails? problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        problem.ShouldNotBeNull();
        problem.Extensions.ContainsKey("code").ShouldBeTrue();
        problem.Extensions["code"]?.ToString().ShouldBe("stock.unit_not_found");
    }

    [Fact]
    public async Task Should_ReturnConflict_WhenUnitAlreadyReserved()
    {
        Guid bankId = await SeedBloodBankAsync("Reserve Conflict Bank");
        DateTimeOffset now = _factory.TimeProvider.GetUtcNow();
        Guid unitId = await SeedBloodUnitAsync(bankId, "DN-RES-ALREADY", now.AddDays(-2));

        // Reserve it once
        var firstRequestId = Guid.NewGuid();
        var firstRequest = new ReserveUnitsRequest(firstRequestId, [unitId]);
        HttpResponseMessage firstResponse = await _client.PostAsJsonAsync(
            "/api/v1/stock/reserve",
            firstRequest
        );
        firstResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        // Try to reserve with different requestId
        var secondRequestId = Guid.NewGuid();
        var secondRequest = new ReserveUnitsRequest(secondRequestId, [unitId]);
        HttpResponseMessage secondResponse = await _client.PostAsJsonAsync(
            "/api/v1/stock/reserve",
            secondRequest
        );

        secondResponse.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        ProblemDetails? problem = await secondResponse.Content.ReadFromJsonAsync<ProblemDetails>();
        problem.ShouldNotBeNull();
        problem.Extensions["code"]?.ToString().ShouldBe("reservation.unavailable");
    }

    [Fact]
    public async Task Should_ReturnOk_WhenRequestIsIdempotentRetry()
    {
        Guid bankId = await SeedBloodBankAsync("Reserve Idempotent Bank");
        DateTimeOffset now = _factory.TimeProvider.GetUtcNow();
        Guid unitId = await SeedBloodUnitAsync(bankId, "DN-RES-IDEM", now.AddDays(-2));

        var requestId = Guid.NewGuid();
        var request = new ReserveUnitsRequest(requestId, [unitId]);

        HttpResponseMessage firstResponse = await _client.PostAsJsonAsync(
            "/api/v1/stock/reserve",
            request
        );
        firstResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        // Repeat with identical requestId and unitIds
        HttpResponseMessage retryResponse = await _client.PostAsJsonAsync(
            "/api/v1/stock/reserve",
            request
        );
        retryResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        ReserveUnitsResponse? body =
            await retryResponse.Content.ReadFromJsonAsync<ReserveUnitsResponse>();
        body.ShouldNotBeNull();
        body.RequestId.ShouldBe(requestId);
        body.ReservedUnitIds.ShouldContain(unitId);

        // Ensure version is not double incremented
        await using InventoryDbContext dbContext = _factory.CreateDbContext();
        BloodUnit? unitInDb = await dbContext.BloodUnits.FindAsync(unitId);
        unitInDb.ShouldNotBeNull();
        unitInDb.Version.ShouldBe(2);
    }

    [Fact]
    public async Task Should_GuaranteeAllOrNothing_WhenOneUnitInBatchIsUnavailable()
    {
        Guid bankId = await SeedBloodBankAsync("Reserve All-or-Nothing Bank");
        DateTimeOffset now = _factory.TimeProvider.GetUtcNow();
        Guid unitAvailableId = await SeedBloodUnitAsync(bankId, "DN-AON-01", now.AddDays(-2));
        Guid unitReservedId = await SeedBloodUnitAsync(bankId, "DN-AON-02", now.AddDays(-2));

        // Already reserve unitReservedId by another request
        var otherRequestId = Guid.NewGuid();
        await _client.PostAsJsonAsync(
            "/api/v1/stock/reserve",
            new ReserveUnitsRequest(otherRequestId, [unitReservedId])
        );

        // Request reservation of both
        var requestId = Guid.NewGuid();
        var request = new ReserveUnitsRequest(requestId, [unitAvailableId, unitReservedId]);
        HttpResponseMessage response = await _client.PostAsJsonAsync(
            "/api/v1/stock/reserve",
            request
        );

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);

        // Verify unitAvailableId remains Available (all-or-nothing: zero partial reservations!)
        await using InventoryDbContext dbContext = _factory.CreateDbContext();
        BloodUnit? unitAvailable = await dbContext.BloodUnits.FindAsync(unitAvailableId);
        unitAvailable.ShouldNotBeNull();
        unitAvailable.Status.ShouldBe(BloodUnitStatus.Available);
        unitAvailable.CurrentReservationRequestId.ShouldBeNull();
        unitAvailable.Version.ShouldBe(1);
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

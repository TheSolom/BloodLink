using System.Net;
using System.Net.Http.Json;
using BloodLink.Inventory.Api.Features.Reservations.ReserveUnits;
using BloodLink.Inventory.Api.Infrastructure.Persistence;
using BloodLink.Inventory.Domain.Model.BloodBanks;
using BloodLink.Inventory.Domain.Model.BloodUnits;
using BloodLink.Inventory.Domain.ValueObjects;
using BloodLink.Inventory.IntegrationTests.Fixtures;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Xunit;

namespace BloodLink.Inventory.IntegrationTests;

[Collection("InventoryIntegrationTests")]
public class ReservationConcurrencyTests : IAsyncLifetime
{
    private readonly DatabaseFixture _databaseFixture;
    private readonly InventoryApiFactory _factory;
    private readonly HttpClient _client;

    public ReservationConcurrencyTests(DatabaseFixture databaseFixture)
    {
        _databaseFixture = databaseFixture;
        _factory = new InventoryApiFactory(databaseFixture);
        _client = _factory.CreateClient();
    }

    public async Task InitializeAsync() => await _databaseFixture.ResetDatabaseAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Should_CorrectlyHandle50ConcurrentReservations_Across10Units_WithZeroDoubleReservations_Repeated20Times()
    {
        Guid bankId = await SeedBloodBankAsync("Concurrency Race Bank");
        DateTimeOffset now = _factory.TimeProvider.GetUtcNow();

        const int iterations = 20;
        const int unitCount = 10;
        const int requestsPerUnit = 5;
        const int totalRequests = unitCount * requestsPerUnit; // 50 requests

        for (int iter = 0; iter < iterations; iter++)
        {
            // Seed 10 distinct available units for this iteration
            var unitIds = new List<Guid>(unitCount);
            for (int u = 0; u < unitCount; u++)
            {
                Guid unitId = await SeedBloodUnitAsync(
                    bankId,
                    $"DN-C50-{iter:D2}-{u:D2}",
                    now.AddDays(-5)
                );
                unitIds.Add(unitId);
            }

            // Prepare 50 concurrent requests: 5 competing requests for each of the 10 units
            var requests = new List<(Guid RequestId, ReserveUnitsRequest Payload)>(totalRequests);
            for (int u = 0; u < unitCount; u++)
            {
                Guid targetUnitId = unitIds[u];
                for (int r = 0; r < requestsPerUnit; r++)
                {
                    var reqId = Guid.NewGuid();
                    requests.Add((reqId, new ReserveUnitsRequest(reqId, [targetUnitId])));
                }
            }

            // Shuffle requests to interleave competing targets
            var rnd = new Random(iter + 42);
            var shuffledRequests = requests.OrderBy(_ => rnd.Next()).ToList();

            // Execute all 50 requests concurrently
            IEnumerable<Task<(Guid RequestId, Guid, HttpStatusCode StatusCode)>> tasks =
                shuffledRequests.Select(async req =>
                {
                    HttpResponseMessage response = await _client.PostAsJsonAsync(
                        "/api/v1/stock/reserve",
                        req.Payload
                    );
                    return (req.RequestId, req.Payload.UnitIds[0], response.StatusCode);
                });

            (Guid RequestId, Guid UnitId, HttpStatusCode StatusCode)[] results = await Task.WhenAll(
                tasks
            );

            // Assert exactly 10 successes (200 OK) and exactly 40 rejections (409 Conflict)
            int successCount = results.Count(r => r.StatusCode == HttpStatusCode.OK);
            int conflictCount = results.Count(r => r.StatusCode == HttpStatusCode.Conflict);

            successCount.ShouldBe(
                unitCount,
                $"Iteration {iter}: Expected exactly {unitCount} successful reservations but got {successCount}"
            );
            conflictCount.ShouldBe(
                totalRequests - unitCount,
                $"Iteration {iter}: Expected exactly {totalRequests - unitCount} conflicts but got {conflictCount}"
            );

            // Verify each of the 10 units has exactly one winning request
            var winningRequests = results.Where(r => r.StatusCode == HttpStatusCode.OK).ToList();

            winningRequests.Count.ShouldBe(unitCount);
            winningRequests.Select(w => w.UnitId).Distinct().Count().ShouldBe(unitCount);

            // Verify database state: all 10 units are Reserved with correct version and reservation request IDs
            await using InventoryDbContext dbContext = _factory.CreateDbContext();
            List<BloodUnit> dbUnits = await dbContext
                .BloodUnits.Where(u => unitIds.Contains(u.Id))
                .ToListAsync();

            dbUnits.Count.ShouldBe(unitCount);
            foreach (BloodUnit unit in dbUnits)
            {
                unit.Status.ShouldBe(
                    BloodUnitStatus.Reserved,
                    $"Iteration {iter}: Unit {unit.Id} should be Reserved"
                );
                unit.Version.ShouldBe(2, $"Iteration {iter}: Unit {unit.Id} version should be 2");
                unit.CurrentReservationRequestId.ShouldNotBeNull(
                    $"Iteration {iter}: Unit {unit.Id} should have reservation request ID"
                );

                // Zero double reservations: CurrentReservationRequestId must match the winning request
                Guid expectedWinner = winningRequests.First(w => w.UnitId == unit.Id).RequestId;
                unit.CurrentReservationRequestId.ShouldBe(expectedWinner);
            }

            // Ensure all 10 reservation request IDs in the DB are strictly unique
            dbUnits
                .Select(u => u.CurrentReservationRequestId!.Value)
                .Distinct()
                .Count()
                .ShouldBe(unitCount);
        }
    }

    [Fact]
    public async Task Should_GuaranteeAllOrNothing_WhenTwoOverlappingReservationsRace()
    {
        Guid bankId = await SeedBloodBankAsync("Overlapping Race Bank");
        DateTimeOffset now = _factory.TimeProvider.GetUtcNow();

        // Seed 3 units: Unit A, Unit B (shared/contested), Unit C
        Guid unitA = await SeedBloodUnitAsync(bankId, "DN-OVR-A", now.AddDays(-3));
        Guid unitB = await SeedBloodUnitAsync(bankId, "DN-OVR-B", now.AddDays(-3));
        Guid unitC = await SeedBloodUnitAsync(bankId, "DN-OVR-C", now.AddDays(-3));

        var req1Id = Guid.NewGuid();
        var req2Id = Guid.NewGuid();

        // Request 1 wants [A, B]
        var req1 = new ReserveUnitsRequest(req1Id, [unitA, unitB]);
        // Request 2 wants [B, C]
        var req2 = new ReserveUnitsRequest(req2Id, [unitB, unitC]);

        // Launch both concurrently
        Task<HttpResponseMessage> task1 = _client.PostAsJsonAsync("/api/v1/stock/reserve", req1);
        Task<HttpResponseMessage> task2 = _client.PostAsJsonAsync("/api/v1/stock/reserve", req2);

        HttpResponseMessage[] responses = await Task.WhenAll(task1, task2);

        // Exactly one must succeed and the other must fail with Conflict
        int successCount = responses.Count(r => r.StatusCode == HttpStatusCode.OK);
        int conflictCount = responses.Count(r => r.StatusCode == HttpStatusCode.Conflict);

        successCount.ShouldBe(
            1,
            "Exactly one of the overlapping reservation requests must succeed"
        );
        conflictCount.ShouldBe(
            1,
            "The competing overlapping reservation request must be rejected with 409 Conflict"
        );

        await using InventoryDbContext dbContext = _factory.CreateDbContext();
        BloodUnit dbUnitA = (await dbContext.BloodUnits.FindAsync(unitA))!;
        BloodUnit dbUnitB = (await dbContext.BloodUnits.FindAsync(unitB))!;
        BloodUnit dbUnitC = (await dbContext.BloodUnits.FindAsync(unitC))!;

        dbUnitB.Status.ShouldBe(BloodUnitStatus.Reserved);
        dbUnitB.Version.ShouldBe(2);

        if (responses[0].StatusCode == HttpStatusCode.OK)
        {
            // Request 1 succeeded -> A and B are reserved for req1Id; C must remain Available!
            dbUnitA.Status.ShouldBe(BloodUnitStatus.Reserved);
            dbUnitA.CurrentReservationRequestId.ShouldBe(req1Id);
            dbUnitA.Version.ShouldBe(2);

            dbUnitB.CurrentReservationRequestId.ShouldBe(req1Id);

            dbUnitC.Status.ShouldBe(BloodUnitStatus.Available);
            dbUnitC.CurrentReservationRequestId.ShouldBeNull();
            dbUnitC.Version.ShouldBe(1);
        }
        else
        {
            // Request 2 succeeded -> B and C are reserved for req2Id; A must remain Available!
            dbUnitC.Status.ShouldBe(BloodUnitStatus.Reserved);
            dbUnitC.CurrentReservationRequestId.ShouldBe(req2Id);
            dbUnitC.Version.ShouldBe(2);

            dbUnitB.CurrentReservationRequestId.ShouldBe(req2Id);

            dbUnitA.Status.ShouldBe(BloodUnitStatus.Available);
            dbUnitA.CurrentReservationRequestId.ShouldBeNull();
            dbUnitA.Version.ShouldBe(1);
        }
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

using BloodLink.Inventory.Contracts;
using BloodLink.Requests.Api.Infrastructure.Clients;
using BloodLink.Requests.Api.Infrastructure.Persistence;
using BloodLink.Requests.Api.Infrastructure.Persistence.ReadModels;
using BloodLink.Requests.Domain.Matching;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Xunit;

namespace BloodLink.Requests.UnitTests.ReadModel;

public class StockReadModelServiceTests
{
    private static RequestsDbContext CreateInMemoryDbContext()
    {
        DbContextOptions<RequestsDbContext> options =
            new DbContextOptionsBuilder<RequestsDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

        return new RequestsDbContext(options);
    }

    [Fact]
    public async Task ApplyUnitReceived_DuplicateEvent_IsIdempotentAndIgnored()
    {
        // Arrange
        using RequestsDbContext dbContext = CreateInMemoryDbContext();
        var service = new StockReadModelService(dbContext);
        var unitId = Guid.NewGuid();
        var bankId = Guid.NewGuid();
        DateTimeOffset now = DateTimeOffset.UtcNow;

        var initialEvent = new UnitReceivedIntegrationEvent(
            unitId,
            "O+",
            "RedCells",
            now.AddDays(-1),
            now.AddDays(30),
            bankId,
            Version: 1
        );

        // Act
        await service.ApplyUnitReceivedAsync(initialEvent);
        var firstCount = await dbContext.StockReadModels.CountAsync();

        // Duplicate event with same version
        await service.ApplyUnitReceivedAsync(initialEvent);
        var secondCount = await dbContext.StockReadModels.CountAsync();

        // Assert
        firstCount.ShouldBe(1);
        secondCount.ShouldBe(1);
        StockItemReadModel stored = await dbContext.StockReadModels.FirstAsync(u =>
            u.UnitId == unitId
        );
        stored.Version.ShouldBe(1);
        stored.BloodType.ShouldBe("O+");
    }

    [Fact]
    public async Task ApplyUnitStatusChanged_StaleEvent_IsIgnored()
    {
        // Arrange
        using RequestsDbContext dbContext = CreateInMemoryDbContext();
        var service = new StockReadModelService(dbContext);
        var unitId = Guid.NewGuid();
        var bankId = Guid.NewGuid();
        DateTimeOffset now = DateTimeOffset.UtcNow;

        // Unit received at version 2 with status Available
        var received = new UnitReceivedIntegrationEvent(
            unitId,
            "A+",
            "RedCells",
            now.AddDays(-1),
            now.AddDays(25),
            bankId,
            Version: 2
        );
        await service.ApplyUnitReceivedAsync(received);

        // Act: Apply status changed with older version (1)
        var staleEvent = new UnitStatusChangedIntegrationEvent(unitId, "Reserved", Version: 1);
        await service.ApplyUnitStatusChangedAsync(staleEvent);

        // Assert: Stored unit status remains Available and version remains 2
        StockItemReadModel stored = await dbContext.StockReadModels.FirstAsync(u =>
            u.UnitId == unitId
        );
        stored.Status.ShouldBe("Available");
        stored.Version.ShouldBe(2);
    }

    [Fact]
    public async Task OutOfOrder_UnitStatusChangedBeforeUnitReceived_ParksIncompleteRowAndCompletesOnUnitReceived()
    {
        // Arrange
        using RequestsDbContext dbContext = CreateInMemoryDbContext();
        var service = new StockReadModelService(dbContext);
        var unitId = Guid.NewGuid();
        var bankId = Guid.NewGuid();
        DateTimeOffset now = DateTimeOffset.UtcNow;

        // Act 1: UnitStatusChanged arrives first (e.g. Reserved, version 2)
        var statusEvent = new UnitStatusChangedIntegrationEvent(unitId, "Reserved", Version: 2);
        await service.ApplyUnitStatusChangedAsync(statusEvent);

        // Assert 1: Parked incomplete row
        StockItemReadModel parked = await dbContext.StockReadModels.FirstAsync(u =>
            u.UnitId == unitId
        );
        parked.Status.ShouldBe("Reserved");
        parked.Version.ShouldBe(2);
        parked.IsComplete.ShouldBeFalse();
        parked.IsMatchCandidate.ShouldBeFalse();

        // Act 2: UnitReceived arrives later with version 2
        var receivedEvent = new UnitReceivedIntegrationEvent(
            unitId,
            "B-",
            "Platelets",
            now.AddDays(-1),
            now.AddDays(5),
            bankId,
            Version: 2
        );
        await service.ApplyUnitReceivedAsync(receivedEvent);

        // Assert 2: Row is completed with attributes, status remains Reserved
        StockItemReadModel completed = await dbContext.StockReadModels.FirstAsync(u =>
            u.UnitId == unitId
        );
        completed.IsComplete.ShouldBeTrue();
        completed.BloodType.ShouldBe("B-");
        completed.Component.ShouldBe("Platelets");
        completed.Status.ShouldBe("Reserved");
        completed.IsMatchCandidate.ShouldBeFalse(); // Not Available
    }

    [Fact]
    public async Task MergeSnapshot_ArrivingAfterNewerEvents_DoesNotOverwriteNewerEventData()
    {
        // Arrange
        using RequestsDbContext dbContext = CreateInMemoryDbContext();
        var service = new StockReadModelService(dbContext);
        var unitId = Guid.NewGuid();
        var bankId = Guid.NewGuid();
        DateTimeOffset now = DateTimeOffset.UtcNow;

        // Live event arrived first at version 3 with status "Delivered"
        var unitReceived = new UnitReceivedIntegrationEvent(
            unitId,
            "O-",
            "RedCells",
            now.AddDays(-2),
            now.AddDays(20),
            bankId,
            Version: 1
        );
        await service.ApplyUnitReceivedAsync(unitReceived);

        var statusEvent = new UnitStatusChangedIntegrationEvent(unitId, "Delivered", Version: 3);
        await service.ApplyUnitStatusChangedAsync(statusEvent);

        // Act: Snapshot arrives later with older version (version 2, status "Reserved")
        var olderSnapshot = new StockSnapshotUnitDto(
            unitId,
            bankId,
            Latitude: 51.5074,
            Longitude: -0.1278,
            "O-",
            "RedCells",
            now.AddDays(20),
            Status: "Reserved",
            Version: 2
        );
        await service.MergeSnapshotItemAsync(olderSnapshot);

        // Assert: Newer event data (Delivered, version 3) was NOT overwritten
        StockItemReadModel stored = await dbContext.StockReadModels.FirstAsync(u =>
            u.UnitId == unitId
        );
        stored.Status.ShouldBe("Delivered");
        stored.Version.ShouldBe(3);
        stored.IsMatchCandidate.ShouldBeFalse();
    }

    [Fact]
    public async Task UnknownStatusString_IsNotMatchCandidate()
    {
        // Arrange
        using RequestsDbContext dbContext = CreateInMemoryDbContext();
        var service = new StockReadModelService(dbContext);
        var unitId = Guid.NewGuid();
        var bankId = Guid.NewGuid();
        DateTimeOffset now = DateTimeOffset.UtcNow;

        var snapshot = new StockSnapshotUnitDto(
            unitId,
            bankId,
            Latitude: 51.5074,
            Longitude: -0.1278,
            "AB+",
            "Plasma",
            now.AddDays(10),
            Status: "QuarantinedUnknownStatus",
            Version: 1
        );

        // Act
        await service.MergeSnapshotItemAsync(snapshot);

        // Assert
        StockItemReadModel stored = await dbContext.StockReadModels.FirstAsync(u =>
            u.UnitId == unitId
        );
        stored.IsComplete.ShouldBeTrue();
        stored.IsMatchCandidate.ShouldBeFalse();

        IReadOnlyList<CandidateUnitSnapshot> candidates =
            await service.GetAvailableCandidateUnitsAsync();
        candidates.Any(c => c.UnitId == unitId).ShouldBeFalse();
    }

    [Fact]
    public async Task UnitReleased_AfterBeingReservedAtSnapshotTime_BecomesMatchCandidate()
    {
        // Arrange
        using RequestsDbContext dbContext = CreateInMemoryDbContext();
        var service = new StockReadModelService(dbContext);
        var unitId = Guid.NewGuid();
        var bankId = Guid.NewGuid();
        DateTimeOffset now = DateTimeOffset.UtcNow;

        // Snapshot has unit at Reserved with version 1
        var snapshot = new StockSnapshotUnitDto(
            unitId,
            bankId,
            Latitude: 51.5074,
            Longitude: -0.1278,
            "O+",
            "RedCells",
            now.AddDays(20),
            Status: "Reserved",
            Version: 1
        );
        await service.MergeSnapshotItemAsync(snapshot);

        StockItemReadModel beforeRelease = await dbContext.StockReadModels.FirstAsync(u =>
            u.UnitId == unitId
        );
        beforeRelease.IsMatchCandidate.ShouldBeFalse();

        // Act: UnitStatusChanged to Available with version 2 (released)
        var releaseEvent = new UnitStatusChangedIntegrationEvent(unitId, "Available", Version: 2);
        await service.ApplyUnitStatusChangedAsync(releaseEvent);

        // Assert: Unit is now a match candidate
        StockItemReadModel afterRelease = await dbContext.StockReadModels.FirstAsync(u =>
            u.UnitId == unitId
        );
        afterRelease.Status.ShouldBe("Available");
        afterRelease.Version.ShouldBe(2);
        afterRelease.IsMatchCandidate.ShouldBeTrue();

        IReadOnlyList<CandidateUnitSnapshot> candidates =
            await service.GetAvailableCandidateUnitsAsync();
        candidates.Any(c => c.UnitId == unitId).ShouldBeTrue();
    }
}

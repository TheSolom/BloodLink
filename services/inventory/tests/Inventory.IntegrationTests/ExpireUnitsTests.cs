using BloodLink.Inventory.Api.Features.Expiry.ExpireUnits;
using BloodLink.Inventory.Api.Infrastructure.Persistence;
using BloodLink.Inventory.Domain.Model.BloodBanks;
using BloodLink.Inventory.Domain.Model.BloodUnits;
using BloodLink.Inventory.Domain.ValueObjects;
using BloodLink.Inventory.IntegrationTests.Fixtures;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace BloodLink.Inventory.IntegrationTests;

[Collection("InventoryIntegrationTests")]
public class ExpireUnitsTests : IAsyncLifetime
{
    private readonly DatabaseFixture _databaseFixture;
    private readonly InventoryApiFactory _factory;

    public ExpireUnitsTests(DatabaseFixture databaseFixture)
    {
        _databaseFixture = databaseFixture;
        _factory = new InventoryApiFactory(databaseFixture);
    }

    public async Task InitializeAsync() => await _databaseFixture.ResetDatabaseAsync();

    public async Task DisposeAsync() => await _factory.DisposeAsync();

    [Fact]
    public async Task Should_TransitionExpiredUnits_To_ExpiredStatus_WhenSweepExecutes()
    {
        Guid bankId = await SeedBloodBankAsync("Expiry Test Bank");
        DateTimeOffset now = _factory.TimeProvider.GetUtcNow();

        // Unit 1: Collected 45 days ago (42-day shelf life -> expired 3 days ago)
        Guid unitExpiredAvailableId = await SeedBloodUnitAsync(
            bankId,
            "DN-EXP-01",
            now.AddDays(-45),
            BloodUnitStatus.Available
        );

        // Unit 2: Collected 2 days ago (expires in 40 days -> still available)
        Guid unitValidAvailableId = await SeedBloodUnitAsync(
            bankId,
            "DN-EXP-02",
            now.AddDays(-2),
            BloodUnitStatus.Available
        );

        // Unit 3: Collected 44 days ago, currently Reserved (shelf life expired 2 days ago -> must transition to Expired)
        Guid unitExpiredReservedId = await SeedBloodUnitAsync(
            bankId,
            "DN-EXP-03",
            now.AddDays(-44),
            BloodUnitStatus.Reserved
        );

        // Unit 4: Collected 44 days ago, but already Delivered (terminal state -> must NOT transition to Expired)
        Guid unitDeliveredId = await SeedBloodUnitAsync(
            bankId,
            "DN-EXP-04",
            now.AddDays(-44),
            BloodUnitStatus.Delivered
        );

        using (IServiceScope scope = _factory.Services.CreateScope())
        {
            ExpireUnitsHandler handler =
                scope.ServiceProvider.GetRequiredService<ExpireUnitsHandler>();

            int sweepCount = await handler.ExecuteSweepAsync(CancellationToken.None);

            sweepCount.ShouldBe(2);
        }

        await using InventoryDbContext dbContext = _factory.CreateDbContext();
        BloodUnit? unit1 = await dbContext.BloodUnits.FindAsync(unitExpiredAvailableId);
        unit1.ShouldNotBeNull();
        unit1.Status.ShouldBe(BloodUnitStatus.Expired);
        unit1.Version.ShouldBe(2);

        BloodUnit? unit2 = await dbContext.BloodUnits.FindAsync(unitValidAvailableId);
        unit2.ShouldNotBeNull();
        unit2.Status.ShouldBe(BloodUnitStatus.Available);
        unit2.Version.ShouldBe(1);

        BloodUnit? unit3 = await dbContext.BloodUnits.FindAsync(unitExpiredReservedId);
        unit3.ShouldNotBeNull();
        unit3.Status.ShouldBe(BloodUnitStatus.Expired);
        unit3.Version.ShouldBe(3);

        BloodUnit? unit4 = await dbContext.BloodUnits.FindAsync(unitDeliveredId);
        unit4.ShouldNotBeNull();
        unit4.Status.ShouldBe(BloodUnitStatus.Delivered);
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
        DateTimeOffset collectedAt,
        BloodUnitStatus targetStatus
    )
    {
        await using InventoryDbContext dbContext = _factory.CreateDbContext();

        // We create unit using reflection / constructor or domain methods
        var unit = BloodUnit.ReceiveDonation(
            Guid.NewGuid(),
            DonationNumber.Create(donationNumber),
            BloodType.Parse("O-"),
            BloodComponent.RedCells,
            collectedAt,
            bankId,
            collectedAt.AddDays(1) // dummy 'now' at collection time
        );

        if (targetStatus == BloodUnitStatus.Reserved)
        {
            unit.Reserve(Guid.NewGuid(), collectedAt.AddDays(2));
        }
        else if (targetStatus == BloodUnitStatus.Delivered)
        {
            var reqId = Guid.NewGuid();
            unit.Reserve(reqId, collectedAt.AddDays(2));
            unit.MarkInTransit(reqId);
            unit.MarkDelivered(reqId);
        }

        dbContext.BloodUnits.Add(unit);
        await dbContext.SaveChangesAsync();
        return unit.Id;
    }
}

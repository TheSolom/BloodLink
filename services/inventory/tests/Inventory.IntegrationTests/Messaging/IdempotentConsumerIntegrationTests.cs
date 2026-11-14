using BloodLink.Inventory.Api.Infrastructure.Persistence;
using BloodLink.Inventory.Contracts;
using BloodLink.Inventory.Domain.Model.BloodBanks;
using BloodLink.Inventory.Domain.Model.BloodUnits;
using BloodLink.Inventory.Domain.ValueObjects;
using BloodLink.Inventory.IntegrationTests.Fixtures;
using MassTransit;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace BloodLink.Inventory.IntegrationTests.Messaging;

[Collection("InventoryIntegrationTests")]
public class IdempotentConsumerIntegrationTests : IAsyncLifetime
{
    private readonly DatabaseFixture _databaseFixture;
    private readonly InventoryApiFactory _factory;

    public IdempotentConsumerIntegrationTests(DatabaseFixture databaseFixture)
    {
        _databaseFixture = databaseFixture;
        _factory = new InventoryApiFactory(databaseFixture);
    }

    public async Task InitializeAsync()
    {
        // Force server startup so MassTransit bus and consumers start listening
        _factory.CreateClient();
        await _databaseFixture.ResetDatabaseAsync();
    }

    public async Task DisposeAsync() => await _factory.DisposeAsync();

    [Fact]
    public async Task Should_DeduplicateDuplicateCommand_ViaEfCoreInbox_AndExecuteOnlyOnce()
    {
        // 1. Seed bank and unit
        Guid bankId = await SeedBloodBankAsync("Inbox Test Bank");
        DateTimeOffset now = _factory.TimeProvider.GetUtcNow();
        Guid unitId = await SeedBloodUnitAsync(bankId, "DN-INBOX-001", now.AddDays(-2));

        IBus bus = _factory.Services.GetRequiredService<IBus>();
        ISendEndpoint sendEndpoint = await bus.GetSendEndpoint(new Uri("queue:reserve-units"));

        var requestId = Guid.NewGuid();
        var messageId = Guid.NewGuid();
        var command = new ReserveUnitsCommand(requestId, [unitId]);

        // 2. Send command first time with MessageId = messageId
        await sendEndpoint.Send(
            command,
            ctx =>
            {
                ctx.MessageId = messageId;
            }
        );

        // 3. Poll until unit is Reserved (or timeout after 15s)
        BloodUnit? reservedUnit = null;
        DateTime deadline = DateTime.UtcNow.AddSeconds(15);
        while (DateTime.UtcNow < deadline)
        {
            await using InventoryDbContext db = _factory.CreateDbContext();
            reservedUnit = await db
                .BloodUnits.AsNoTracking()
                .FirstOrDefaultAsync(u => u.Id == unitId);
            if (reservedUnit?.Status == BloodUnitStatus.Reserved)
            {
                break;
            }
            await Task.Delay(250);
        }

        reservedUnit.ShouldNotBeNull();
        reservedUnit.Status.ShouldBe(BloodUnitStatus.Reserved);
        reservedUnit.Version.ShouldBe(2);
        reservedUnit.CurrentReservationRequestId.ShouldBe(requestId);

        // 4. Verify InboxStates recorded the MessageId
        await using var connection = new SqlConnection(_databaseFixture.ConnectionString);
        await connection.OpenAsync();
        await using var inboxCmd = new SqlCommand(
            "SELECT COUNT(1) FROM inventory.InboxStates WHERE MessageId = @msgId",
            connection
        );
        inboxCmd.Parameters.AddWithValue("@msgId", messageId);
        int inboxCount = (int)(await inboxCmd.ExecuteScalarAsync() ?? 0);
        inboxCount.ShouldBe(1, "EF Core Inbox should record the first processed MessageId");

        // 5. Send duplicate command with the exact same MessageId
        await sendEndpoint.Send(
            command,
            ctx =>
            {
                ctx.MessageId = messageId;
            }
        );

        // Allow consumer time to process duplicate
        await Task.Delay(2000);

        // 6. Verify unit version has NOT changed and remains exactly 2 (zero double execution)
        await using InventoryDbContext verifyDb = _factory.CreateDbContext();
        BloodUnit unitAfterDuplicate = (await verifyDb.BloodUnits.FindAsync(unitId))!;
        unitAfterDuplicate.Status.ShouldBe(BloodUnitStatus.Reserved);
        unitAfterDuplicate.Version.ShouldBe(
            2,
            "Version must remain 2 after duplicate command delivery"
        );
        unitAfterDuplicate.CurrentReservationRequestId.ShouldBe(requestId);
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

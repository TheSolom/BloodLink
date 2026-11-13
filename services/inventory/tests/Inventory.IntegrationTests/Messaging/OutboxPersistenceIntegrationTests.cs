using System.Net;
using BloodLink.Inventory.Api.Features.Donations.ReceiveDonation;
using BloodLink.Inventory.Api.Infrastructure.Persistence;
using BloodLink.Inventory.Domain.Model.BloodBanks;
using BloodLink.Inventory.Domain.ValueObjects;
using BloodLink.Inventory.IntegrationTests.Fixtures;
using Microsoft.Data.SqlClient;
using Shouldly;
using Xunit;

namespace BloodLink.Inventory.IntegrationTests.Messaging;

[Collection("InventoryIntegrationTests")]
public class OutboxPersistenceIntegrationTests : IAsyncLifetime
{
    private readonly DatabaseFixture _databaseFixture;
    private readonly InventoryApiFactory _factory;
    private readonly HttpClient _client;

    public OutboxPersistenceIntegrationTests(DatabaseFixture databaseFixture)
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
    public async Task Should_PersistOutboxMessages_InSameTransaction_WhenReceivingDonation()
    {
        // 1. Seed blood bank
        Guid bankId = await SeedBloodBankAsync("Outbox Test Bank");
        DateTimeOffset now = _factory.TimeProvider.GetUtcNow();
        DateTimeOffset collectedAt = now.AddDays(-1);

        var request = new ReceiveDonationRequest(
            bankId,
            "DN-OUTBOX-001",
            "O-",
            "RedCells",
            collectedAt
        );

        // 2. Post donation
        HttpResponseMessage response = await _client.PostAsJsonAsync("/api/v1/donations", request);
        response.StatusCode.ShouldBe(HttpStatusCode.Created);

        // 3. Inspect SQL Server database in schema 'inventory' to confirm outbox records
        await using var connection = new SqlConnection(_databaseFixture.ConnectionString);
        await connection.OpenAsync();

        // Query OutboxMessages and OutboxStates to confirm transactional outbox persistence
        await using var messageCmd = new SqlCommand(
            "SELECT COUNT(1) FROM inventory.OutboxMessages WHERE MessageType LIKE '%UnitReceivedIntegrationEvent%'",
            connection
        );
        int messageCount = (int)(await messageCmd.ExecuteScalarAsync() ?? 0);

        await using var stateCmd = new SqlCommand(
            "SELECT COUNT(1) FROM inventory.OutboxStates",
            connection
        );
        int stateCount = (int)(await stateCmd.ExecuteScalarAsync() ?? 0);

        // Either the outbox message is still queued or the MassTransit poller already delivered it
        (messageCount > 0 || stateCount > 0).ShouldBeTrue(
            "Expected transactional outbox state or message to be recorded in schema inventory"
        );
    }

    private async Task<Guid> SeedBloodBankAsync(string name)
    {
        await using InventoryDbContext dbContext = _factory.CreateDbContext();
        var bank = BloodBank.Register(Guid.NewGuid(), name, GeoLocation.Create(40.0, -70.0));
        dbContext.BloodBanks.Add(bank);
        await dbContext.SaveChangesAsync();
        return bank.Id;
    }
}

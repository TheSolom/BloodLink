using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using BloodLink.Inventory.Api.Features.Donations.ReceiveDonation;
using BloodLink.Inventory.Domain.Model.BloodBanks;
using BloodLink.Inventory.Domain.ValueObjects;
using BloodLink.Inventory.IntegrationTests.Fixtures;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Xunit;

namespace BloodLink.Inventory.IntegrationTests;

[Collection("InventoryIntegrationTests")]
public class ReceiveDonationTests : IAsyncLifetime
{
    private readonly DatabaseFixture _databaseFixture;
    private readonly InventoryApiFactory _factory;
    private readonly HttpClient _client;

    public ReceiveDonationTests(DatabaseFixture databaseFixture)
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
    public async Task Should_ReceiveDonation_AndCalculateExpiry_Correctly()
    {
        var bankId = await SeedBloodBankAsync("Main Blood Center");
        var now = _factory.TimeProvider.GetUtcNow();
        var collectedAt = now.AddDays(-2);

        var request = new ReceiveDonationRequest(
            bankId,
            "DN-TEST-001",
            "O-",
            "RedCells",
            collectedAt
        );

        var response = await _client.PostAsJsonAsync("/api/v1/donations", request);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<ReceiveDonationResponse>();
        body.ShouldNotBeNull();
        body.Id.ShouldNotBe(Guid.Empty);
        body.DonationNumber.ShouldBe("DN-TEST-001");
        body.BloodType.ShouldBe("O-");
        body.Component.ShouldBe("RedCells");
        body.CollectedAt.ShouldBe(collectedAt.ToUniversalTime());
        body.ExpiresAt.ShouldBe(collectedAt.ToUniversalTime().AddDays(42));
        body.Status.ShouldBe("Available");
        body.BloodBankId.ShouldBe(bankId);

        await using var dbContext = _factory.CreateDbContext();
        var unitInDb = await dbContext.BloodUnits.FindAsync(body.Id);
        unitInDb.ShouldNotBeNull();
        unitInDb.Version.ShouldBe(1);
    }

    [Fact]
    public async Task Should_ReturnNotFound_WithProblemDetails_AndPersistNothing_WhenBankDoesNotExist()
    {
        var nonExistentBankId = Guid.NewGuid();
        var request = new ReceiveDonationRequest(
            nonExistentBankId,
            "DN-404-001",
            "A+",
            "Platelets",
            _factory.TimeProvider.GetUtcNow().AddDays(-1)
        );

        var response = await _client.PostAsJsonAsync("/api/v1/donations", request);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");

        var doc = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        doc.RootElement.GetProperty("status").GetInt32().ShouldBe(404);
        doc.RootElement.GetProperty("code").GetString().ShouldBe("blood_bank.not_found");

        // Verify nothing was persisted to database
        await using var dbContext = _factory.CreateDbContext();
        var unitsCount = await dbContext.BloodUnits.CountAsync();
        unitsCount.ShouldBe(0);
    }

    [Fact]
    public async Task Should_ReturnBadRequest_WhenInvalidBloodType()
    {
        var bankId = await SeedBloodBankAsync("North Center");
        var request = new ReceiveDonationRequest(
            bankId,
            "DN-INV-001",
            "INVALID_TYPE",
            "RedCells",
            _factory.TimeProvider.GetUtcNow().AddDays(-1)
        );

        var response = await _client.PostAsJsonAsync("/api/v1/donations", request);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var doc = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        doc.RootElement.GetProperty("code").GetString().ShouldBe("validation.failed");
        doc.RootElement.GetProperty("errors").TryGetProperty("BloodType", out _).ShouldBeTrue();
    }

    [Fact]
    public async Task Should_ReturnConflict_WhenDuplicateDonationNumber_SubmittedSequentially()
    {
        var bankId = await SeedBloodBankAsync("East Center");
        var request = new ReceiveDonationRequest(
            bankId,
            "DN-DUP-001",
            "B+",
            "RedCells",
            _factory.TimeProvider.GetUtcNow().AddDays(-1)
        );

        var firstResponse = await _client.PostAsJsonAsync("/api/v1/donations", request);
        firstResponse.StatusCode.ShouldBe(HttpStatusCode.Created);

        var secondResponse = await _client.PostAsJsonAsync("/api/v1/donations", request);
        secondResponse.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        secondResponse.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");

        var doc = await JsonDocument.ParseAsync(await secondResponse.Content.ReadAsStreamAsync());
        doc.RootElement.GetProperty("status").GetInt32().ShouldBe(409);
        doc.RootElement.GetProperty("code").GetString().ShouldBe("donation.duplicate");
    }

    [Fact]
    public async Task Should_ReturnOneSuccess_AndOneConflict_WhenDuplicateDonationNumber_SubmittedConcurrently()
    {
        var bankId = await SeedBloodBankAsync("West Center");
        var request = new ReceiveDonationRequest(
            bankId,
            "DN-CONCUR-001",
            "AB+",
            "Platelets",
            _factory.TimeProvider.GetUtcNow().AddDays(-1)
        );

        using var client2 = _factory.CreateClient();

        var task1 = _client.PostAsJsonAsync("/api/v1/donations", request);
        var task2 = client2.PostAsJsonAsync("/api/v1/donations", request);

        var responses = await Task.WhenAll(task1, task2);

        var statuses = responses.Select(r => r.StatusCode).ToList();
        statuses.ShouldContain(HttpStatusCode.Created);
        statuses.ShouldContain(HttpStatusCode.Conflict);

        var conflictResponse = responses.First(r => r.StatusCode == HttpStatusCode.Conflict);
        var doc = await JsonDocument.ParseAsync(await conflictResponse.Content.ReadAsStreamAsync());
        doc.RootElement.GetProperty("status").GetInt32().ShouldBe(409);
        doc.RootElement.GetProperty("code").GetString().ShouldBe("donation.duplicate");

        // Exactly one unit in database
        await using var dbContext = _factory.CreateDbContext();
        var count = await dbContext.BloodUnits.CountAsync(u => u.BloodBankId == bankId && u.DonationNumber == DonationNumber.Create("DN-CONCUR-001"));
        count.ShouldBe(1);
    }

    [Fact]
    public async Task Should_AllowSameDonationNumber_InDifferentBloodBanks()
    {
        var bankAId = await SeedBloodBankAsync("Bank Alpha");
        var bankBId = await SeedBloodBankAsync("Bank Beta");

        var requestA = new ReceiveDonationRequest(
            bankAId,
            "DN-SHARED-001",
            "O+",
            "RedCells",
            _factory.TimeProvider.GetUtcNow().AddDays(-1)
        );

        var requestB = new ReceiveDonationRequest(
            bankBId,
            "DN-SHARED-001",
            "O+",
            "RedCells",
            _factory.TimeProvider.GetUtcNow().AddDays(-1)
        );

        var responseA = await _client.PostAsJsonAsync("/api/v1/donations", requestA);
        var responseB = await _client.PostAsJsonAsync("/api/v1/donations", requestB);

        responseA.StatusCode.ShouldBe(HttpStatusCode.Created);
        responseB.StatusCode.ShouldBe(HttpStatusCode.Created);

        await using var dbContext = _factory.CreateDbContext();
        var count = await dbContext.BloodUnits.CountAsync(u => u.DonationNumber == DonationNumber.Create("DN-SHARED-001"));
        count.ShouldBe(2);
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


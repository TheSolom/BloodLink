using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using BloodLink.Inventory.Api.Features.BloodBanks.RegisterBloodBank;
using BloodLink.Inventory.IntegrationTests.Fixtures;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Xunit;

namespace BloodLink.Inventory.IntegrationTests;

[Collection("InventoryIntegrationTests")]
public class RegisterBloodBankTests : IAsyncLifetime
{
    private readonly DatabaseFixture _databaseFixture;
    private readonly InventoryApiFactory _factory;
    private readonly HttpClient _client;

    public RegisterBloodBankTests(DatabaseFixture databaseFixture)
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
    public async Task Should_RegisterBank_Successfully_WhenValidRequest()
    {
        var request = new RegisterBloodBankRequest("Metropolitan Blood Bank", 40.7128, -74.0060);

        var response = await _client.PostAsJsonAsync("/api/v1/banks", request);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<RegisterBloodBankResponse>();
        body.ShouldNotBeNull();
        body.Id.ShouldNotBe(Guid.Empty);
        body.Name.ShouldBe("Metropolitan Blood Bank");
        body.Latitude.ShouldBe(40.7128);
        body.Longitude.ShouldBe(-74.0060);

        await using var dbContext = _factory.CreateDbContext();
        var bankInDb = await dbContext.BloodBanks.FindAsync(body.Id);
        bankInDb.ShouldNotBeNull();
        bankInDb.Name.ShouldBe("Metropolitan Blood Bank");
    }

    [Fact]
    public async Task Should_ReturnBadRequest_WithProblemDetails_WhenInvalidCoordinates()
    {
        var request = new RegisterBloodBankRequest("Invalid Coordinates Bank", 120.0, -74.0060);

        var response = await _client.PostAsJsonAsync("/api/v1/banks", request);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");

        var doc = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        var root = doc.RootElement;

        root.GetProperty("status").GetInt32().ShouldBe(400);
        root.GetProperty("code").GetString().ShouldBe("validation.failed");
        root.TryGetProperty("errors", out var errors).ShouldBeTrue();
        errors.TryGetProperty("Latitude", out var latErrors).ShouldBeTrue();
        latErrors.GetArrayLength().ShouldBeGreaterThan(0);
    }
}


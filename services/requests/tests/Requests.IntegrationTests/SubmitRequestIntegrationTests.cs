using System.Net;
using BloodLink.Requests.Api.Features.Requests.SubmitRequest;
using BloodLink.Requests.Api.Infrastructure.Persistence;
using BloodLink.Requests.Domain.BloodRequests;
using BloodLink.Requests.IntegrationTests.Fixtures;
using Shouldly;
using Xunit;

namespace BloodLink.Requests.IntegrationTests;

[Collection("RequestsIntegration")]
public class SubmitRequestIntegrationTests : IAsyncLifetime
{
    private readonly DatabaseFixture _databaseFixture;
    private readonly RequestsApiFactory _factory;
    private readonly HttpClient _client;

    public SubmitRequestIntegrationTests(DatabaseFixture databaseFixture)
    {
        _databaseFixture = databaseFixture;
        _factory = new RequestsApiFactory(databaseFixture);
        _client = _factory.CreateClient();
    }

    public async Task InitializeAsync() => await _databaseFixture.ResetDatabaseAsync();

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _factory.DisposeAsync();
    }

    [Fact]
    public async Task SubmitRequest_EndToEnd_PersistsToSqlAndReturns200()
    {
        // Arrange
        var command = new SubmitRequestCommand(
            "O-",
            "RedCells",
            UnitsNeeded: 2,
            "Critical",
            _factory.TimeProvider.GetUtcNow().AddHours(2)
        );

        var requestMessage = new HttpRequestMessage(HttpMethod.Post, "/api/v1/requests")
        {
            Content = JsonContent.Create(command),
        };
        requestMessage.Headers.Add("Idempotency-Key", "e2e-submit-key-1");

        // Act
        HttpResponseMessage response = await _client.SendAsync(requestMessage);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        SubmitRequestResponse? body =
            await response.Content.ReadFromJsonAsync<SubmitRequestResponse>();
        body.ShouldNotBeNull();
        body.BloodType.ShouldBe("O-");
        body.UnitsNeeded.ShouldBe(2);
        body.Status.ShouldBe("Submitted");

        await using RequestsDbContext dbContext = _factory.CreateDbContext();
        BloodRequest? stored = await dbContext.BloodRequests.FindAsync(body.Id);
        stored.ShouldNotBeNull();
        stored.HospitalId.ShouldBe(SeededHospitals.StThomas.Id);
    }

    [Fact]
    public async Task SubmitRequest_MissingIdempotencyKey_Returns400ProblemDetails()
    {
        // Arrange
        var command = new SubmitRequestCommand(
            "A+",
            "Platelets",
            UnitsNeeded: 1,
            "Routine",
            _factory.TimeProvider.GetUtcNow().AddHours(4)
        );

        // Act: without Idempotency-Key header
        HttpResponseMessage response = await _client.PostAsJsonAsync("/api/v1/requests", command);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }
}

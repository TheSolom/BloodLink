using System.Net;
using System.Net.Http.Json;
using BloodLink.Requests.Api.Features.Requests.SubmitRequest;
using BloodLink.Requests.Api.Infrastructure.Persistence;
using BloodLink.Requests.Domain.Hospitals;
using BloodLink.Requests.IntegrationTests.Fixtures;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Xunit;

namespace BloodLink.Requests.IntegrationTests;

[Collection("RequestsIntegration")]
public class SubmitRequestIntegrationTests(
    RequestsApiFactory factory,
    DatabaseFixture databaseFixture
) : IClassFixture<RequestsApiFactory>, IClassFixture<DatabaseFixture>, IAsyncLifetime
{
    private readonly HttpClient _client = factory.CreateClient();

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        await databaseFixture.ResetDatabaseAsync();
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
            factory.TimeProvider.GetUtcNow().AddHours(2)
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

        await using var dbContext = factory.CreateDbContext();
        var stored = await dbContext.BloodRequests.FindAsync(body.Id);
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
            factory.TimeProvider.GetUtcNow().AddHours(4)
        );

        // Act: without Idempotency-Key header
        HttpResponseMessage response = await _client.PostAsJsonAsync("/api/v1/requests", command);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }
}

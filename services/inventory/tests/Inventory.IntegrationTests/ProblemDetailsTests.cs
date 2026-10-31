using System.Net;
using System.Text.Json;
using BloodLink.Inventory.IntegrationTests.Fixtures;
using Shouldly;
using Xunit;

namespace BloodLink.Inventory.IntegrationTests;

[Collection("InventoryIntegrationTests")]
public class ProblemDetailsTests
{
    private readonly InventoryApiFactory _factory;
    private readonly HttpClient _client;

    public ProblemDetailsTests(DatabaseFixture databaseFixture)
    {
        _factory = new InventoryApiFactory(databaseFixture);
        _client = _factory.CreateClient();
    }

    [Fact]
    public async Task Should_MapDomainException_To422ProblemDetails_WithStableCode()
    {
        HttpResponseMessage response = await _client.PostAsync(
            "/test/throw-domain-exception",
            null
        );

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");

        JsonDocument doc = await JsonDocument.ParseAsync(
            await response.Content.ReadAsStreamAsync()
        );
        JsonElement root = doc.RootElement;

        root.GetProperty("status").GetInt32().ShouldBe(422);
        root.GetProperty("title").GetString().ShouldBe("Domain Rule Violation");
        root.GetProperty("detail").GetString().ShouldBe("Sample domain rule failure.");
        root.GetProperty("code").GetString().ShouldBe("sample.domain_rule_violated");
    }
}

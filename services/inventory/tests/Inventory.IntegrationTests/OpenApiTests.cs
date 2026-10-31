using System.Net;
using System.Text.Json;
using BloodLink.Inventory.IntegrationTests.Fixtures;
using Shouldly;
using Xunit;

namespace BloodLink.Inventory.IntegrationTests;

[Collection("InventoryIntegrationTests")]
public class OpenApiTests(DatabaseFixture databaseFixture)
{
    private readonly InventoryApiFactory _factory = new(databaseFixture);

    [Fact]
    public async Task GetOpenApi_Returns200AndValidDocument()
    {
        using HttpClient client = _factory.CreateClient();
        HttpResponseMessage response = await client.GetAsync("/openapi/v1.json");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();

        using var doc = JsonDocument.Parse(content);
        doc.RootElement.TryGetProperty("openapi", out JsonElement openapiProp).ShouldBeTrue();
        openapiProp.GetString().ShouldStartWith("3.");

        // Also save OpenAPI spec to a known path for tooling if folder exists
        var targetDir = Path.GetFullPath(
            Path.Combine(AppContext.BaseDirectory, "../../../../../../../web/dashboard")
        );
        if (!Directory.Exists(targetDir))
        {
            Directory.CreateDirectory(targetDir);
        }
        await File.WriteAllTextAsync(Path.Combine(targetDir, "openapi.json"), content);
    }
}

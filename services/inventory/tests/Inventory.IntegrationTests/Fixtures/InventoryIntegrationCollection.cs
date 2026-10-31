using Xunit;

namespace BloodLink.Inventory.IntegrationTests.Fixtures;

[CollectionDefinition("InventoryIntegrationTests")]
public class InventoryIntegrationCollection : ICollectionFixture<DatabaseFixture>
{
}


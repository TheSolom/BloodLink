using Xunit;

namespace BloodLink.Requests.IntegrationTests.Fixtures;

[CollectionDefinition("RequestsIntegration")]
public class RequestsIntegrationCollection : ICollectionFixture<DatabaseFixture> { }

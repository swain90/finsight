using Xunit;

namespace FinSight.Api.IntegrationTests;

[CollectionDefinition("Integration")]
public class IntegrationTestCollection : ICollectionFixture<FinSight.Api.IntegrationTests.Helpers.FinSightWebApplicationFactory>;

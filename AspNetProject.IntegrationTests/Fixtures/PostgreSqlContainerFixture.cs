using Testcontainers.PostgreSql;
using Xunit;

namespace AspNetProject.IntegrationTests.Fixtures;

public class PostgreSqlContainerFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:16-alpine")
        .WithDatabase("testdb")
        .WithUsername("testuser")
        .WithPassword("testpassword")
        .Build();

    public string GetConnectionString() => _container.GetConnectionString();

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
    }

    public async Task DisposeAsync()
    {
        await _container.StopAsync();
        await _container.DisposeAsync();
    }
}

[CollectionDefinition("DatabaseCollection")]
public class DatabaseCollection : ICollectionFixture<PostgreSqlContainerFixture>
{
}
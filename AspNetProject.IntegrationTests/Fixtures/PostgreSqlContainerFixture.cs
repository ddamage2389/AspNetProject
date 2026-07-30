using Testcontainers.PostgreSql;
using Xunit;

namespace AspNetProject.IntegrationTests.Fixtures;

public class PostgreSqlContainerFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("testdb")
        .WithUsername("testuser")
        .WithPassword("testpassword")
        .Build();

    public string GetConnectionString() => _container.GetConnectionString();

    public async Task InitializeAsync()
    {
        // Запускаем контейнер перед всеми тестами
        await _container.StartAsync();
    }

    public async Task DisposeAsync()
    {
        // Останавливаем и удаляем контейнер после всех тестов
        await _container.StopAsync();
        await _container.DisposeAsync();
    }
}

// Определяем коллекцию тестов, чтобы использовать одну фикстуру для всех классов
[CollectionDefinition("DatabaseCollection")]
public class DatabaseCollection : ICollectionFixture<PostgreSqlContainerFixture>
{
    // связь имени коллекции с фикстурой
}
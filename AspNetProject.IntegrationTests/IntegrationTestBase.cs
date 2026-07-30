using AspNetProject.DataAccess;
using AspNetProject.IntegrationTests.Fixtures;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AspNetProject.IntegrationTests;

[Collection("DatabaseCollection")] // Привязываемся к нашей коллекции с одним контейнером
public abstract class IntegrationTestBase : IAsyncLifetime
{
    protected readonly AppDbContext DbContext;
    private readonly PostgreSqlContainerFixture _fixture;

    protected IntegrationTestBase(PostgreSqlContainerFixture fixture)
    {
        _fixture = fixture;

        // Настраиваем DbContext на использование реального PostgreSQL из Testcontainers
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(_fixture.GetConnectionString())
            .Options;

        DbContext = new AppDbContext(options);
    }

    public async Task InitializeAsync()
    {
        // Перед каждым тестом: удаляем старую БД и применяем миграции заново
        // Это гарантирует абсолютно чистое состояние и проверяет, что миграции работают!
        await DbContext.Database.EnsureDeletedAsync();
        await DbContext.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        await DbContext.DisposeAsync();
    }
}
using AspNetProject.Bookings.Infrastructure;
using AspNetProject.Events.Infrastructure.DataAccess;
using AspNetProject.Users.Infrastructure.DataAccess;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Testcontainers.PostgreSql;

namespace AspNetProject.Sprint9.Tests;

public sealed class DatabaseFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:16-alpine").Build();

    public string Connection(string database) =>
        new NpgsqlConnectionStringBuilder(_container.GetConnectionString()) { Database = database }.ConnectionString;

    public EventsDbContext Events() => new(new DbContextOptionsBuilder<EventsDbContext>().UseNpgsql(Connection("events")).Options);
    public BookingsDbContext Bookings() => new(new DbContextOptionsBuilder<BookingsDbContext>().UseNpgsql(Connection("bookings")).Options);
    public UsersDbContext Users() => new(new DbContextOptionsBuilder<UsersDbContext>().UseNpgsql(Connection("users")).Options);

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        await using var connection = new NpgsqlConnection(_container.GetConnectionString());
        await connection.OpenAsync();
        foreach (var name in new[] { "users", "events", "bookings" })
        {
            await using var command = new NpgsqlCommand($"CREATE DATABASE {name}", connection);
            await command.ExecuteNonQueryAsync();
        }
        await using var users = Users();
        await using var events = Events();
        await using var bookings = Bookings();
        await users.Database.MigrateAsync();
        await events.Database.MigrateAsync();
        await bookings.Database.MigrateAsync();
    }

    public async Task DisposeAsync() => await _container.DisposeAsync();
}

[CollectionDefinition("Sprint9")]
public sealed class DatabaseCollection : ICollectionFixture<DatabaseFixture> { }

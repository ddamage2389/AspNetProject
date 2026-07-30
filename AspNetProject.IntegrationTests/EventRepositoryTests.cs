using AspNetProject.IntegrationTests.Fixtures;
using AspNetProject.Models;
using AspNetProject.Repositories;
using FluentAssertions;
using Xunit;

namespace AspNetProject.IntegrationTests;

public class EventRepositoryTests : IntegrationTestBase
{
    private readonly EventRepository _repository;

    public EventRepositoryTests(PostgreSqlContainerFixture fixture) : base(fixture)
    {
        _repository = new EventRepository(DbContext);
    }

    [Fact]
    public async Task AddAsync_And_GetByIdAsync_ShouldWork()
    {
        // Arrange
        var newEvent = Event.Create("Тестовое событие", "Описание", DateTime.UtcNow, DateTime.UtcNow.AddHours(2), 50);

        // Act
        await _repository.AddAsync(newEvent);
        await _repository.SaveChangesAsync();

        var foundEvent = await _repository.GetByIdAsync(newEvent.Id);

        // Assert
        foundEvent.Should().NotBeNull();
        foundEvent!.Title.Should().Be("Тестовое событие");
        foundEvent.AvailableSeats.Should().Be(50);
    }

    [Fact]
    public async Task GetAllAsync_ShouldSupportPaginationAndFiltering()
    {
        // Arrange
        var date = new DateTime(2026, 10, 1, 10, 0, 0, DateTimeKind.Utc);
        await _repository.AddAsync(Event.Create("Митап по C#", "Desc", date, date.AddHours(2), 10));
        await _repository.AddAsync(Event.Create("Митап по Python", "Desc", date, date.AddHours(2), 10));
        await _repository.AddAsync(Event.Create("Конференция", "Desc", date.AddDays(5), date.AddDays(5).AddHours(2), 10));
        await _repository.SaveChangesAsync();

        // Act: ищем "митап" с пагинацией (страница 1, размер 1)
        var result = await _repository.GetAllAsync(title: "митап", from: date, to: date.AddDays(1), page: 1, pageSize: 1);

        // Assert
        result.TotalCount.Should().Be(2);
        result.Items.Should().HaveCount(1);
        result.Items.First().Title.Should().Contain("Митап");
    }

    [Fact]
    public async Task UpdateAsync_ShouldModifyEvent()
    {
        // Arrange
        var ev = Event.Create("Старое название", "Desc", DateTime.UtcNow, DateTime.UtcNow.AddHours(1), 10);
        await _repository.AddAsync(ev);
        await _repository.SaveChangesAsync();

        // Act
        ev.Update("Новое название", "Новое описание", ev.StartAt, ev.EndAt);
        await _repository.UpdateAsync(ev);
        await _repository.SaveChangesAsync();

        var updated = await _repository.GetByIdAsync(ev.Id);

        // Assert
        updated.Should().NotBeNull();
        updated!.Title.Should().Be("Новое название");
        updated.Description.Should().Be("Новое описание");
    }

    [Fact]
    public async Task DeleteAsync_ShouldRemoveEvent()
    {
        // Arrange
        var ev = Event.Create("Удали меня", "Desc", DateTime.UtcNow, DateTime.UtcNow.AddHours(1), 10);
        await _repository.AddAsync(ev);
        await _repository.SaveChangesAsync();

        // Act
        await _repository.DeleteAsync(ev.Id);
        await _repository.SaveChangesAsync();

        var found = await _repository.GetByIdAsync(ev.Id);

        // Assert
        found.Should().BeNull();
    }
}
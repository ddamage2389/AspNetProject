using AspNetProject.Dtos;
using AspNetProject.Models;
using AspNetProject.Services;
using FluentAssertions;
using Xunit;

namespace AspNetProject.Tests;

public class EventServiceTests
{
    private EventService CreateService() => new EventService();

    #region CRUD: Успешные сценарии

    [Fact]
    public async Task CreateAsync_ShouldAddEventAndReturnItWithNewId()
    {
        var service = CreateService();
        var newEvent = Event.Create(
            title: "Тест",
            description: "Описание",
            startAt: DateTime.Now,
            endAt: DateTime.Now.AddHours(1),
            totalSeats: 10
        );

        var result = await service.CreateAsync(newEvent);

        result.Should().NotBeNull();
        result.Title.Should().Be("Тест");
        result.TotalSeats.Should().Be(10);
        result.AvailableSeats.Should().Be(10);
    }

    [Fact]
    public async Task GetAllAsync_ShouldReturnAllCreatedEvents()
    {
        var service = CreateService();
        await service.CreateAsync(new Event { Title = "Событие 1", StartAt = DateTime.Now, EndAt = DateTime.Now.AddHours(1) });
        await service.CreateAsync(new Event { Title = "Событие 2", StartAt = DateTime.Now, EndAt = DateTime.Now.AddHours(1) });

        var result = await service.GetAllAsync();
        result.Items.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetByIdAsync_ShouldReturnEvent_WhenExists()
    {
        var service = CreateService();
        var created = await service.CreateAsync(new Event { Title = "Найти меня", StartAt = DateTime.Now, EndAt = DateTime.Now.AddHours(1) });

        var found = await service.GetByIdAsync(created.Id);

        found.Should().NotBeNull();
        found.Id.Should().Be(created.Id);
    }

    [Fact]
    public async Task UpdateAsync_ShouldUpdateFields_WhenExists()
    {
        var service = CreateService();
        var created = await service.CreateAsync(new Event { Title = "Старое название", StartAt = DateTime.Now, EndAt = DateTime.Now.AddHours(1) });
        var updateDto = new Event { Title = "Новое название", Description = "Обновлено", StartAt = created.StartAt, EndAt = created.EndAt };

        var updated = await service.UpdateAsync(created.Id, updateDto);

        updated.Should().NotBeNull();
        updated.Title.Should().Be("Новое название");
        updated.Description.Should().Be("Обновлено");
    }

    [Fact]
    public async Task DeleteAsync_ShouldReturnTrueAndRemoveEvent_WhenExists()
    {
        var service = CreateService();
        var created = await service.CreateAsync(new Event { Title = "Удали меня", StartAt = DateTime.Now, EndAt = DateTime.Now.AddHours(1) });

        var deleted = await service.DeleteAsync(created.Id);
        var afterDelete = await service.GetByIdAsync(created.Id);

        deleted.Should().BeTrue();
        afterDelete.Should().BeNull();
    }

    #endregion

    #region CRUD: Неуспешные сценарии

    [Fact]
    public async Task GetByIdAsync_ShouldReturnNull_WhenNotExists()
    {
        var service = CreateService();
        var fakeId = Guid.NewGuid();

        var result = await service.GetByIdAsync(fakeId);

        result.Should().BeNull();
    }

    [Fact]
    public async Task UpdateAsync_ShouldReturnNull_WhenNotExists()
    {
        var service = CreateService();
        var fakeId = Guid.NewGuid();
        var updateDto = new Event { Title = "Неважно", StartAt = DateTime.Now, EndAt = DateTime.Now.AddHours(1) };

        var result = await service.UpdateAsync(fakeId, updateDto);

        result.Should().BeNull();
    }

    [Fact]
    public async Task DeleteAsync_ShouldReturnFalse_WhenNotExists()
    {
        var service = CreateService();
        var fakeId = Guid.NewGuid();

        var result = await service.DeleteAsync(fakeId);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task UpdateAsync_ShouldThrowException_WhenEndAtIsBeforeStartAt()
    {
        // Arrange
        var service = CreateService();
        var created = await service.CreateAsync(new Event
        {
            Title = "Тестовое событие",
            StartAt = DateTime.Now,
            EndAt = DateTime.Now.AddHours(1)
        });

        var invalidUpdate = new Event
        {
            Title = "Обновлённое",
            StartAt = DateTime.Now.AddHours(2),
            EndAt = DateTime.Now 
        };

        // Act & Assert
        await Assert.ThrowsAsync<AspNetProject.Exceptions.InvalidEventDatesException>(
            () => service.UpdateAsync(created.Id, invalidUpdate)
        );
    }

    #endregion

    #region Фильтрация и Пагинация

    [Fact]
    public async Task GetAllAsync_ShouldFilterByTitle_IgnoreCase()
    {
        var service = CreateService();
        await service.CreateAsync(new Event { Title = "Митап по C#", StartAt = DateTime.Now, EndAt = DateTime.Now.AddHours(1) });
        await service.CreateAsync(new Event { Title = "Конференция", StartAt = DateTime.Now, EndAt = DateTime.Now.AddHours(1) });
        await service.CreateAsync(new Event { Title = "митап по Python", StartAt = DateTime.Now, EndAt = DateTime.Now.AddHours(1) });

        var result = await service.GetAllAsync(title: "митап");

        result.Items.Should().HaveCount(2);
        result.Items.All(e => e.Title.Contains("митап", StringComparison.OrdinalIgnoreCase)).Should().BeTrue();
    }

    [Fact]
    public async Task GetAllAsync_ShouldFilterByDates()
    {
        var service = CreateService();
        var baseDate = new DateTime(2026, 6, 15, 10, 0, 0);

        await service.CreateAsync(new Event { Title = "Раннее", StartAt = baseDate.AddDays(-5), EndAt = baseDate.AddDays(-4) });
        await service.CreateAsync(new Event { Title = "В диапазоне", StartAt = baseDate, EndAt = baseDate.AddHours(2) });
        await service.CreateAsync(new Event { Title = "Позднее", StartAt = baseDate.AddDays(5), EndAt = baseDate.AddDays(6) });

        var result = await service.GetAllAsync(from: baseDate, to: baseDate.AddDays(1));

        result.Items.Should().HaveCount(1);
        result.Items.First().Title.Should().Be("В диапазоне");
    }

    [Fact]
    public async Task GetAllAsync_ShouldSupportPagination()
    {
        var service = CreateService();
        for (int i = 1; i <= 15; i++)
        {
            await service.CreateAsync(new Event { Title = $"Событие {i}", StartAt = DateTime.Now, EndAt = DateTime.Now.AddHours(1) });
        }

        var result = await service.GetAllAsync(page: 2, pageSize: 5);

        result.TotalCount.Should().Be(15);
        result.Page.Should().Be(2);
        result.PageSize.Should().Be(5);
        result.Items.Should().HaveCount(5);
        result.Items.First().Title.Should().Be("Событие 6");
    }

    [Fact]
    public async Task GetAllAsync_ShouldCombineFiltersAndPagination()
    {
        var service = CreateService();
        var date = DateTime.Now;
        await service.CreateAsync(new Event { Title = "Митап 1", StartAt = date, EndAt = date.AddHours(1) });
        await service.CreateAsync(new Event { Title = "Митап 2", StartAt = date, EndAt = date.AddHours(1) });
        await service.CreateAsync(new Event { Title = "Конференция", StartAt = date, EndAt = date.AddHours(1) });

        var result = await service.GetAllAsync(title: "митап", page: 2, pageSize: 1);

        result.TotalCount.Should().Be(2);
        result.Items.Should().HaveCount(1);
        result.Items.First().Title.Should().Be("Митап 2");
    }

    #endregion
}
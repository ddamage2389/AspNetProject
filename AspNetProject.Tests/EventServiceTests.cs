using AspNetProject.DataAccess;
using AspNetProject.Exceptions;
using AspNetProject.Models;
using AspNetProject.Repositories;
using AspNetProject.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AspNetProject.Tests;

public class EventServiceTests : IDisposable
{
    private readonly string _dbName;
    private readonly ServiceProvider _serviceProvider;

    public EventServiceTests()
    {
        _dbName = Guid.NewGuid().ToString();
        var services = new ServiceCollection();

        services.AddDbContext<AppDbContext>(options =>
            options.UseInMemoryDatabase(_dbName));

        services.AddScoped<IEventRepository, EventRepository>();
        services.AddScoped<IEventService, EventService>();

        _serviceProvider = services.BuildServiceProvider();
    }

    public void Dispose()
    {
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        context.Database.EnsureDeleted();
        _serviceProvider.Dispose();
    }

    private IEventService CreateService()
    {
        var scope = _serviceProvider.CreateScope();
        return scope.ServiceProvider.GetRequiredService<IEventService>();
    }

    #region CRUD: Успешные сценарии

    [Fact]
    public async Task CreateAsync_ShouldAddEventAndReturnItWithNewId()
    {
        var service = CreateService();
        var newEvent = Event.Create("Тест", "Описание", DateTime.Now, DateTime.Now.AddHours(1), 10);

        var result = await service.CreateAsync(newEvent);

        result.Should().NotBeNull();
        result.Id.Should().NotBe(Guid.Empty);
        result.Title.Should().Be("Тест");
        result.TotalSeats.Should().Be(10);
        result.AvailableSeats.Should().Be(10);
    }

    [Fact]
    public async Task GetAllAsync_ShouldReturnAllCreatedEvents()
    {
        var service = CreateService();
        await service.CreateAsync(Event.Create("Событие 1", "D", DateTime.Now, DateTime.Now.AddHours(1), 10));
        await service.CreateAsync(Event.Create("Событие 2", "D", DateTime.Now, DateTime.Now.AddHours(1), 10));

        var result = await service.GetAllAsync();
        result.Items.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetByIdAsync_ShouldReturnEvent_WhenExists()
    {
        var service = CreateService();
        var created = await service.CreateAsync(Event.Create("Найти меня", "D", DateTime.Now, DateTime.Now.AddHours(1), 10));

        var found = await service.GetByIdAsync(created.Id);

        found.Should().NotBeNull();
        found.Id.Should().Be(created.Id);
    }

    [Fact]
    public async Task UpdateAsync_ShouldUpdateFields_WhenExists()
    {
        var service = CreateService();
        var created = await service.CreateAsync(Event.Create("Старое название", "D", DateTime.Now, DateTime.Now.AddHours(1), 10));

        var existing = await service.GetByIdAsync(created.Id);
        existing!.Update("Новое название", "Обновлено", created.StartAt, created.EndAt);

        var updated = await service.UpdateAsync(created.Id, existing);

        updated.Should().NotBeNull();
        updated.Title.Should().Be("Новое название");
        updated.Description.Should().Be("Обновлено");
    }

    [Fact]
    public async Task DeleteAsync_ShouldReturnTrueAndRemoveEvent_WhenExists()
    {
        var service = CreateService();
        var created = await service.CreateAsync(Event.Create("Удали меня", "D", DateTime.Now, DateTime.Now.AddHours(1), 10));

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
        var result = await service.GetByIdAsync(Guid.NewGuid());
        result.Should().BeNull();
    }

    [Fact]
    public async Task UpdateAsync_ShouldReturnNull_WhenNotExists()
    {
        var service = CreateService();
        var fakeEvent = Event.Create("x", "D", DateTime.Now, DateTime.Now.AddHours(1), 10);
        var result = await service.UpdateAsync(Guid.NewGuid(), fakeEvent);
        result.Should().BeNull();
    }

    [Fact]
    public async Task DeleteAsync_ShouldReturnFalse_WhenNotExists()
    {
        var service = CreateService();
        var result = await service.DeleteAsync(Guid.NewGuid());
        result.Should().BeFalse();
    }

    //[Fact]
    public async Task UpdateAsync_ShouldThrowException_WhenEndAtIsBeforeStartAt()
    {
        var service = CreateService();
        var created = await service.CreateAsync(Event.Create("Тест", "D", DateTime.Now, DateTime.Now.AddHours(1), 10));

        var invalidUpdate = Event.Create("x", "D", DateTime.Now.AddHours(2), DateTime.Now, 10);

        await Assert.ThrowsAsync<InvalidEventDatesException>(
            () => service.UpdateAsync(created.Id, invalidUpdate)
        );
    }

    #endregion

    #region Фильтрация и Пагинация

    [Fact]
    public async Task GetAllAsync_ShouldFilterByTitle_IgnoreCase()
    {
        var service = CreateService();
        await service.CreateAsync(Event.Create("Митап по C#", "D", DateTime.Now, DateTime.Now.AddHours(1), 10));
        await service.CreateAsync(Event.Create("Конференция", "D", DateTime.Now, DateTime.Now.AddHours(1), 10));
        await service.CreateAsync(Event.Create("митап по Python", "D", DateTime.Now, DateTime.Now.AddHours(1), 10));

        var result = await service.GetAllAsync(title: "митап");

        result.Items.Should().HaveCount(2);
        result.Items.All(e => e.Title.Contains("митап", StringComparison.OrdinalIgnoreCase)).Should().BeTrue();
    }

    [Fact]
    public async Task GetAllAsync_ShouldFilterByDates()
    {
        var service = CreateService();
        var baseDate = new DateTime(2026, 6, 15, 10, 0, 0);

        await service.CreateAsync(Event.Create("Раннее", "D", baseDate.AddDays(-5), baseDate.AddDays(-4), 10));
        await service.CreateAsync(Event.Create("В диапазоне", "D", baseDate, baseDate.AddHours(2), 10));
        await service.CreateAsync(Event.Create("Позднее", "D", baseDate.AddDays(5), baseDate.AddDays(6), 10));

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
            await service.CreateAsync(Event.Create($"Событие {i}", "D", DateTime.Now, DateTime.Now.AddHours(1), 10));
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
        await service.CreateAsync(Event.Create("Митап 1", "D", date, date.AddHours(1), 10));
        await service.CreateAsync(Event.Create("Митап 2", "D", date, date.AddHours(1), 10));
        await service.CreateAsync(Event.Create("Конференция", "D", date, date.AddHours(1), 10));

        var result = await service.GetAllAsync(title: "митап", page: 2, pageSize: 1);

        result.TotalCount.Should().Be(2);
        result.Items.Should().HaveCount(1);
        result.Items.First().Title.Should().Be("Митап 2");
    }

    #endregion
}
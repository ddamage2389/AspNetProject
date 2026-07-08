using AspNetProject.DataAccess;
using AspNetProject.Dtos;
using AspNetProject.Models;
using AspNetProject.Services;
using FluentAssertions;
using Xunit;

namespace AspNetProject.Tests;

// Вспомогательный класс, имитирующий EventService для тестов
public class FakeEventService : IEventService
{
    private readonly List<Event> _events = new();

    public void AddEvent(Event e) => _events.Add(e);

    public Task<Event?> GetByIdAsync(Guid id)
    {
        var e = _events.FirstOrDefault(x => x.Id == id);
        return Task.FromResult(e);
    }

    public Task<PaginatedResult<Event>> GetAllAsync(string? title = null, DateTime? from = null, DateTime? to = null, int page = 1, int pageSize = 10)
    {
        // Возвращаем заглушку с пагинацией
        return Task.FromResult(new PaginatedResult<Event>
        {
            Items = _events.AsEnumerable(),
            TotalCount = _events.Count,
            Page = page,
            PageSize = pageSize
        });
    }

    public Task<Event> CreateAsync(Event @event)
    {
        _events.Add(@event);
        return Task.FromResult(@event);
    }

    public Task<Event> UpdateAsync(Guid id, Event @event)
    {
        var existing = _events.FirstOrDefault(x => x.Id == id);
        if (existing != null)
        {
            var index = _events.IndexOf(existing);
            _events[index] = @event;
            return Task.FromResult(@event);
        }
        throw new KeyNotFoundException();
    }

    public Task<bool> DeleteAsync(Guid id)
    {
        var existing = _events.FirstOrDefault(x => x.Id == id);
        if (existing != null)
        {
            _events.Remove(existing);
            return Task.FromResult(true);
        }
        return Task.FromResult(false);
    }
}

public class BookingServiceTests
{
    private readonly InMemoryBookingStore _store;
    private readonly FakeEventService _fakeEventService;
    private readonly BookingService _sut;

    public BookingServiceTests()
    {
        // ARRANGE: Подготовка окружения перед каждым тестом
        _store = new InMemoryBookingStore();
        _fakeEventService = new FakeEventService();
        _sut = new BookingService(_fakeEventService, _store);
    }

    [Fact]
    public async Task CreateBookingAsync_ShouldReturnBooking_WhenEventExists()
    {
        // Arrange
        var eventId = Guid.NewGuid();
        _fakeEventService.AddEvent(new Event { Id = eventId, Title = "Test", StartAt = DateTime.Now, EndAt = DateTime.Now.AddHours(1) });

        // Act
        var result = await _sut.CreateBookingAsync(eventId);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be(BookingStatus.Pending);
        result.EventId.Should().Be(eventId);
    }

    [Fact]
    public async Task CreateBookingAsync_ShouldThrowKeyNotFoundException_WhenEventDoesNotExist()
    {
        // Arrange
        var nonExistentId = Guid.NewGuid();
        // Ничего не добавляем в _fakeEventService

        // Act & Assert
        await Assert.ThrowsAsync<KeyNotFoundException>(() => _sut.CreateBookingAsync(nonExistentId));
    }

    [Fact]
    public async Task GetBookingByIdAsync_ShouldReturnBooking_WhenExists()
    {
        // Arrange
        var eventId = Guid.NewGuid();
        _fakeEventService.AddEvent(new Event { Id = eventId, Title = "Test", StartAt = DateTime.Now, EndAt = DateTime.Now.AddHours(1) });
        var createdBooking = await _sut.CreateBookingAsync(eventId);

        // Act
        var result = await _sut.GetBookingByIdAsync(createdBooking.Id);

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().Be(createdBooking.Id);
    }

    [Fact]
    public async Task GetBookingByIdAsync_ShouldReturnNull_WhenNotExists()
    {
        // Arrange
        var nonExistentId = Guid.NewGuid();

        // Act
        var result = await _sut.GetBookingByIdAsync(nonExistentId);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task CreateBookingAsync_ShouldCreateUniqueIds_ForSameEvent()
    {
        // Arrange
        var eventId = Guid.NewGuid();
        _fakeEventService.AddEvent(new Event { Id = eventId, Title = "Test", StartAt = DateTime.Now, EndAt = DateTime.Now.AddHours(1) });

        // Act
        var booking1 = await _sut.CreateBookingAsync(eventId);
        var booking2 = await _sut.CreateBookingAsync(eventId);

        // Assert
        booking1.Id.Should().NotBe(booking2.Id);
    }

    [Fact]
    public async Task Booking_ShouldReflectStatusChange_AfterConfirm()
    {
        // Arrange
        var eventId = Guid.NewGuid();
        _fakeEventService.AddEvent(new Event { Id = eventId, Title = "Test", StartAt = DateTime.Now, EndAt = DateTime.Now.AddHours(1) });
        var booking = await _sut.CreateBookingAsync(eventId);

        // Проверяем, что изначально статус Pending
        booking.Status.Should().Be(BookingStatus.Pending);
        booking.ProcessedAt.Should().BeNull();

        // Act: Имитируем работу фонового сервиса (вызываем Confirm напрямую)
        booking.Confirm();

        // Assert
        booking.Status.Should().Be(BookingStatus.Confirmed);
        booking.ProcessedAt.Should().NotBeNull();
        booking.ProcessedAt.Value.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(2));
    }

    [Fact]
    public async Task CreateBookingAsync_ShouldThrowKeyNotFoundException_WhenEventWasDeleted()
    {
        // Arrange
        var eventId = Guid.NewGuid();
        // Сначала добавляем событие
        _fakeEventService.AddEvent(new Event { Id = eventId, Title = "Test", StartAt = DateTime.Now, EndAt = DateTime.Now.AddHours(1) });

        // Затем удаляем его
        await _fakeEventService.DeleteAsync(eventId);

        // Act & Assert
        await Assert.ThrowsAsync<KeyNotFoundException>(() => _sut.CreateBookingAsync(eventId));
    }
}
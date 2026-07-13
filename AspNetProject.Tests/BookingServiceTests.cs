using AspNetProject.DataAccess;
using AspNetProject.Dtos;
using AspNetProject.Exceptions;
using AspNetProject.Models;
using AspNetProject.Services;
using FluentAssertions;
using Xunit;

namespace AspNetProject.Tests;

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
        _store = new InMemoryBookingStore();
        _fakeEventService = new FakeEventService();
        _sut = new BookingService(_fakeEventService, _store);
    }

    [Fact]
    public async Task CreateBookingAsync_ShouldReturnBooking_WhenEventExists()
    {
        var eventId = Guid.NewGuid();
        var eventItem = Event.Create("Test", "Desc", DateTime.Now, DateTime.Now.AddHours(1), 10);
        // Принудительно меняем ID на свой
        eventItem.Id = eventId;
        _fakeEventService.AddEvent(eventItem);

        var result = await _sut.CreateBookingAsync(eventId);

        result.Should().NotBeNull();
        result.Status.Should().Be(BookingStatus.Pending);
        result.EventId.Should().Be(eventId);
    }

    [Fact]
    public async Task CreateBookingAsync_ShouldThrowKeyNotFoundException_WhenEventDoesNotExist()
    {
        var nonExistentId = Guid.NewGuid();
        await Assert.ThrowsAsync<KeyNotFoundException>(() => _sut.CreateBookingAsync(nonExistentId));
    }

    [Fact]
    public async Task GetBookingByIdAsync_ShouldReturnBooking_WhenExists()
    {
        var eventId = Guid.NewGuid();
        var eventItem = Event.Create("Test", "Desc", DateTime.Now, DateTime.Now.AddHours(1), 10);
        eventItem.Id = eventId;
        _fakeEventService.AddEvent(eventItem);

        var createdBooking = await _sut.CreateBookingAsync(eventId);
        var result = await _sut.GetBookingByIdAsync(createdBooking.Id);

        result.Should().NotBeNull();
        result.Id.Should().Be(createdBooking.Id);
    }

    [Fact]
    public async Task GetBookingByIdAsync_ShouldReturnNull_WhenNotExists()
    {
        var nonExistentId = Guid.NewGuid();
        var result = await _sut.GetBookingByIdAsync(nonExistentId);
        result.Should().BeNull();
    }

    [Fact]
    public async Task CreateBookingAsync_ShouldCreateUniqueIds_ForSameEvent()
    {
        var eventId = Guid.NewGuid();
        var eventItem = Event.Create("Test", "Desc", DateTime.Now, DateTime.Now.AddHours(1), 100);
        eventItem.Id = eventId;
        _fakeEventService.AddEvent(eventItem);

        var booking1 = await _sut.CreateBookingAsync(eventId);
        var booking2 = await _sut.CreateBookingAsync(eventId);

        booking1.Id.Should().NotBe(booking2.Id);
    }

    [Fact]
    public async Task Booking_ShouldReflectStatusChange_AfterConfirm()
    {
        var eventId = Guid.NewGuid();
        var eventItem = Event.Create("Test", "Desc", DateTime.Now, DateTime.Now.AddHours(1), 10);
        eventItem.Id = eventId;
        _fakeEventService.AddEvent(eventItem);

        var booking = await _sut.CreateBookingAsync(eventId);

        booking.Status.Should().Be(BookingStatus.Pending);
        booking.ProcessedAt.Should().BeNull();

        booking.Confirm();

        booking.Status.Should().Be(BookingStatus.Confirmed);
        booking.ProcessedAt.Should().NotBeNull();
        booking.ProcessedAt.Value.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(2));
    }

    [Fact]
    public async Task CreateBookingAsync_ShouldThrowKeyNotFoundException_WhenEventWasDeleted()
    {
        var eventId = Guid.NewGuid();
        var eventItem = Event.Create("Test", "Desc", DateTime.Now, DateTime.Now.AddHours(1), 10);
        eventItem.Id = eventId;
        _fakeEventService.AddEvent(eventItem);

        await _fakeEventService.DeleteAsync(eventId);

        await Assert.ThrowsAsync<KeyNotFoundException>(() => _sut.CreateBookingAsync(eventId));
    }

    // ===== ВСПОМОГАТЕЛЬНЫЙ МЕТОД =====
    private Event CreateTestEvent(int totalSeats)
    {
        var @event = Event.Create(
            title: "Test Event",
            description: "Desc",
            startAt: DateTime.UtcNow,
            endAt: DateTime.UtcNow.AddHours(1),
            totalSeats: totalSeats
        );
        _fakeEventService.AddEvent(@event);
        return @event;
    }

    // ===== ТЕСТЫ СПРИНТА 4 =====

    #region Тесты на места (Seats)

    [Fact]
    public async Task CreateBookingAsync_ShouldDecreaseAvailableSeats()
    {
        var eventItem = CreateTestEvent(totalSeats: 1);

        await _sut.CreateBookingAsync(eventItem.Id);

        var updatedEvent = await _fakeEventService.GetByIdAsync(eventItem.Id);

        updatedEvent.Should().NotBeNull();
        updatedEvent.AvailableSeats.Should().Be(0);
    }

    [Fact]
    public async Task CreateBookingAsync_ShouldThrowNoAvailableSeatsException_WhenFull()
    {
        var eventItem = CreateTestEvent(totalSeats: 1);

        await _sut.CreateBookingAsync(eventItem.Id);

        await Assert.ThrowsAsync<NoAvailableSeatsException>(
            () => _sut.CreateBookingAsync(eventItem.Id));
    }

    #endregion

    #region Тесты на смену статуса

    [Fact]
    public void Booking_Confirm_ShouldSetStatusAndProcessedAt()
    {
        var eventItem = CreateTestEvent(1);
        var booking = Booking.CreatePending(eventItem.Id);

        booking.Confirm();

        booking.Status.Should().Be(BookingStatus.Confirmed);
        booking.ProcessedAt.Should().NotBeNull();
        booking.ProcessedAt.Value.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(2));
    }

    [Fact]
    public void Booking_Reject_ShouldReleaseSeats()
    {
        var eventItem = CreateTestEvent(totalSeats: 1);
        var booking = Booking.CreatePending(eventItem.Id);

        var reserved = eventItem.TryReserveSeats(1);
        reserved.Should().BeTrue();
        eventItem.AvailableSeats.Should().Be(0);

        booking.Reject();
        eventItem.ReleaseSeats(1);

        booking.Status.Should().Be(BookingStatus.Rejected);
        eventItem.AvailableSeats.Should().Be(1);
    }

    [Fact]
    public async Task AfterReject_NewBookingShouldBePossible()
    {
        var eventItem = CreateTestEvent(totalSeats: 1);

        var booking = await _sut.CreateBookingAsync(eventItem.Id);

        booking.Reject();

        var eventInStore = await _fakeEventService.GetByIdAsync(eventItem.Id);
        eventInStore?.ReleaseSeats(1);
        await _fakeEventService.UpdateAsync(eventItem.Id, eventInStore!);

        var newBooking = await _sut.CreateBookingAsync(eventItem.Id);

        newBooking.Should().NotBeNull();
        newBooking.Status.Should().Be(BookingStatus.Pending);
    }

    #endregion

    #region Тесты на конкурентность (Concurrency)

    [Fact]
    public async Task Concurrency_ShouldPreventOverbooking()
    {
        var eventItem = CreateTestEvent(totalSeats: 5);
        int successCount = 0;
        int failCount = 0;
        var lockObj = new object();

        var tasks = Enumerable.Range(0, 20).Select(async _ =>
        {
            try
            {
                await _sut.CreateBookingAsync(eventItem.Id);
                lock (lockObj) { successCount++; }
            }
            catch (NoAvailableSeatsException)
            {
                lock (lockObj) { failCount++; }
            }
        });

        await Task.WhenAll(tasks);

        successCount.Should().Be(5);
        failCount.Should().Be(15);

        var updatedEvent = await _fakeEventService.GetByIdAsync(eventItem.Id);
        updatedEvent.AvailableSeats.Should().Be(0);
    }

    [Fact]
    public async Task Concurrency_ShouldCreateUniqueIds()
    {
        var eventItem = CreateTestEvent(totalSeats: 10);
        var createdBookings = new List<Booking>();
        var lockObj = new object();

        var tasks = Enumerable.Range(0, 10).Select(async _ =>
        {
            var booking = await _sut.CreateBookingAsync(eventItem.Id);
            lock (lockObj) { createdBookings.Add(booking); }
        });

        await Task.WhenAll(tasks);

        createdBookings.Should().HaveCount(10);
        createdBookings.Select(b => b.Id).Distinct().Should().HaveCount(10);
    }

    #endregion
}
using AspNetProject.IntegrationTests.Fixtures;
using AspNetProject.Domain.Entities;
using FluentAssertions;
using AspNetProject.Infrastructure.Repositories;
using Xunit;

namespace AspNetProject.IntegrationTests;

public class BookingRepositoryTests : IntegrationTestBase
{
    private readonly BookingRepository _bookingRepository;
    private readonly EventRepository _eventRepository;

    public BookingRepositoryTests(PostgreSqlContainerFixture fixture) : base(fixture)
    {
        _bookingRepository = new BookingRepository(DbContext);
        _eventRepository = new EventRepository(DbContext);
    }

    [Fact]
    public async Task AddAsync_And_GetByIdAsync_ShouldWork()
    {
        // Arrange
        var ev = Event.Create("Событие для брони", "Desc", DateTime.UtcNow, DateTime.UtcNow.AddHours(1), 10);
        await _eventRepository.AddAsync(ev);
        await _eventRepository.SaveChangesAsync();

        var booking = Booking.CreatePending(ev.Id);

        // Act
        await _bookingRepository.AddAsync(booking);
        await _bookingRepository.SaveChangesAsync();

        var foundBooking = await _bookingRepository.GetByIdAsync(booking.Id);

        // Assert
        foundBooking.Should().NotBeNull();
        foundBooking!.Status.Should().Be(BookingStatus.Pending);
        foundBooking.EventId.Should().Be(ev.Id);
    }

    [Fact]
    public async Task GetPendingBookingIdsAsync_ShouldReturnOnlyPendingBookings()
    {
        // Arrange
        var ev = Event.Create("Событие", "Desc", DateTime.UtcNow, DateTime.UtcNow.AddHours(1), 10);
        await _eventRepository.AddAsync(ev);

        var pending1 = Booking.CreatePending(ev.Id);
        var pending2 = Booking.CreatePending(ev.Id);
        var confirmed = Booking.CreatePending(ev.Id);
        confirmed.Confirm(); // Меняем статус

        await _bookingRepository.AddAsync(pending1);
        await _bookingRepository.AddAsync(pending2);
        await _bookingRepository.AddAsync(confirmed);
        await _bookingRepository.SaveChangesAsync();

        // Act
        var pendingIds = await _bookingRepository.GetPendingBookingIdsAsync();

        // Assert
        pendingIds.Should().HaveCount(2);
        pendingIds.Should().Contain(pending1.Id);
        pendingIds.Should().Contain(pending2.Id);
        pendingIds.Should().NotContain(confirmed.Id);
    }
}

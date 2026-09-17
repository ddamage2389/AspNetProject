using AspNetProject.Domain.Entities;
using AspNetProject.Infrastructure.Repositories;
using AspNetProject.IntegrationTests.Fixtures;
using FluentAssertions;
using Microsoft.Extensions.Logging;
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
        var ev = Event.Create("Событие для брони", "Desc", DateTime.UtcNow.AddHours(1), DateTime.UtcNow.AddHours(2), 10);
        await _eventRepository.AddAsync(ev);

        var user = User.Create("testuser", "password_hash", Role.User);
        await DbContext.Users.AddAsync(user);

        await _eventRepository.SaveChangesAsync();

        var booking = Booking.CreatePending(ev.Id, user.Id);

        // Act
        await _bookingRepository.AddAsync(booking);
        await _bookingRepository.SaveChangesAsync();

        var foundBooking = await _bookingRepository.GetByIdAsync(booking.Id);

        // Assert
        foundBooking.Should().NotBeNull();
        foundBooking!.Status.Should().Be(BookingStatus.Pending);
        foundBooking.EventId.Should().Be(ev.Id);
        foundBooking.UserId.Should().Be(user.Id);
    }

    [Fact]
    public async Task GetPendingBookingIdsAsync_ShouldReturnOnlyPendingBookings()
    {
        // Arrange
        var ev = Event.Create("Событие", "Desc", DateTime.UtcNow.AddHours(1), DateTime.UtcNow.AddHours(2), 10);
        await _eventRepository.AddAsync(ev);

        var user = User.Create("testuser2", "password_hash", Role.User);
        await DbContext.Users.AddAsync(user);
        await _eventRepository.SaveChangesAsync();

        var pending1 = Booking.CreatePending(ev.Id, user.Id);
        var pending2 = Booking.CreatePending(ev.Id, user.Id);
        var confirmed = Booking.CreatePending(ev.Id, user.Id);
        confirmed.Confirm(); 

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
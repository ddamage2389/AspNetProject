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

public class BookingServiceTests : IDisposable
{
    private readonly string _dbName;
    private readonly ServiceProvider _serviceProvider;

    public BookingServiceTests()
    {
        _dbName = Guid.NewGuid().ToString();
        var services = new ServiceCollection();

        services.AddDbContext<AppDbContext>(options =>
            options.UseInMemoryDatabase(_dbName));

        services.AddScoped<IEventRepository, EventRepository>();
        services.AddScoped<IBookingRepository, BookingRepository>();

        services.AddScoped<IEventService, EventService>();
        services.AddScoped<IBookingService, BookingService>();

        _serviceProvider = services.BuildServiceProvider();
    }

    public void Dispose()
    {
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        context.Database.EnsureDeleted();
        _serviceProvider.Dispose();
    }

    private async Task<Event> CreateTestEventAsync(int totalSeats)
    {
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var @event = Event.Create("Test Event", "Desc", DateTime.UtcNow, DateTime.UtcNow.AddHours(1), totalSeats);
        context.Events.Add(@event);
        await context.SaveChangesAsync();
        return @event;
    }

    #region Базовые тесты

    [Fact]
    public async Task CreateBookingAsync_ShouldReturnBooking_WhenEventExists()
    {
        var eventItem = await CreateTestEventAsync(10);

        using var scope = _serviceProvider.CreateScope();
        var bookingService = scope.ServiceProvider.GetRequiredService<IBookingService>();
        var result = await bookingService.CreateBookingAsync(eventItem.Id);

        result.Should().NotBeNull();
        result.Status.Should().Be(BookingStatus.Pending);
        result.EventId.Should().Be(eventItem.Id);
    }

    [Fact]
    public async Task CreateBookingAsync_ShouldThrowKeyNotFoundException_WhenEventDoesNotExist()
    {
        var nonExistentId = Guid.NewGuid();

        using var scope = _serviceProvider.CreateScope();
        var bookingService = scope.ServiceProvider.GetRequiredService<IBookingService>();

        await Assert.ThrowsAsync<KeyNotFoundException>(() => bookingService.CreateBookingAsync(nonExistentId));
    }

    [Fact]
    public async Task GetBookingByIdAsync_ShouldReturnBooking_WhenExists()
    {
        var eventItem = await CreateTestEventAsync(10);

        using var scope = _serviceProvider.CreateScope();
        var bookingService = scope.ServiceProvider.GetRequiredService<IBookingService>();
        var createdBooking = await bookingService.CreateBookingAsync(eventItem.Id);
        var result = await bookingService.GetBookingByIdAsync(createdBooking.Id);

        result.Should().NotBeNull();
        result.Id.Should().Be(createdBooking.Id);
    }

    [Fact]
    public async Task GetBookingByIdAsync_ShouldReturnNull_WhenNotExists()
    {
        var nonExistentId = Guid.NewGuid();

        using var scope = _serviceProvider.CreateScope();
        var bookingService = scope.ServiceProvider.GetRequiredService<IBookingService>();
        var result = await bookingService.GetBookingByIdAsync(nonExistentId);

        result.Should().BeNull();
    }

    [Fact]
    public async Task CreateBookingAsync_ShouldCreateUniqueIds_ForSameEvent()
    {
        var eventItem = await CreateTestEventAsync(100);

        using var scope = _serviceProvider.CreateScope();
        var bookingService = scope.ServiceProvider.GetRequiredService<IBookingService>();

        var booking1 = await bookingService.CreateBookingAsync(eventItem.Id);
        var booking2 = await bookingService.CreateBookingAsync(eventItem.Id);

        booking1.Id.Should().NotBe(booking2.Id);
    }

    [Fact]
    public async Task Booking_ShouldReflectStatusChange_AfterConfirm()
    {
        var eventItem = await CreateTestEventAsync(10);

        using var scope = _serviceProvider.CreateScope();
        var bookingService = scope.ServiceProvider.GetRequiredService<IBookingService>();
        var booking = await bookingService.CreateBookingAsync(eventItem.Id);

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
        var eventItem = await CreateTestEventAsync(10);

        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        context.Events.Remove(eventItem);
        await context.SaveChangesAsync();

        var bookingService = scope.ServiceProvider.GetRequiredService<IBookingService>();
        await Assert.ThrowsAsync<KeyNotFoundException>(() => bookingService.CreateBookingAsync(eventItem.Id));
    }

    #endregion

    #region Тесты на места (Seats) — Спринт 4

    [Fact]
    public async Task CreateBookingAsync_ShouldDecreaseAvailableSeats()
    {
        var eventItem = await CreateTestEventAsync(totalSeats: 1);

        using var scope = _serviceProvider.CreateScope();
        var bookingService = scope.ServiceProvider.GetRequiredService<IBookingService>();
        await bookingService.CreateBookingAsync(eventItem.Id);

        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var updatedEvent = await context.Events.FindAsync(eventItem.Id);

        updatedEvent.Should().NotBeNull();
        updatedEvent!.AvailableSeats.Should().Be(0);
    }

    [Fact]
    public async Task CreateBookingAsync_ShouldThrowNoAvailableSeatsException_WhenFull()
    {
        var eventItem = await CreateTestEventAsync(totalSeats: 1);

        using var scope = _serviceProvider.CreateScope();
        var bookingService = scope.ServiceProvider.GetRequiredService<IBookingService>();

        await bookingService.CreateBookingAsync(eventItem.Id);
        await Assert.ThrowsAsync<NoAvailableSeatsException>(() => bookingService.CreateBookingAsync(eventItem.Id));
    }

    #endregion

    #region Тесты на смену статуса

    [Fact]
    public void Booking_Confirm_ShouldSetStatusAndProcessedAt()
    {
        var eventItem = Event.Create("Test", "Desc", DateTime.UtcNow, DateTime.UtcNow.AddHours(1), 1);
        var booking = Booking.CreatePending(eventItem.Id);

        booking.Confirm();

        booking.Status.Should().Be(BookingStatus.Confirmed);
        booking.ProcessedAt.Should().NotBeNull();
        booking.ProcessedAt.Value.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(2));
    }

    [Fact]
    public void Booking_Reject_ShouldReleaseSeats()
    {
        var eventItem = Event.Create("Test", "Desc", DateTime.UtcNow, DateTime.UtcNow.AddHours(1), 1);
        var reserved = eventItem.TryReserveSeats(1);
        reserved.Should().BeTrue();
        eventItem.AvailableSeats.Should().Be(0);

        var booking = Booking.CreatePending(eventItem.Id);
        booking.Reject();
        eventItem.ReleaseSeats(1);

        booking.Status.Should().Be(BookingStatus.Rejected);
        eventItem.AvailableSeats.Should().Be(1);
    }

    [Fact]
    public async Task AfterReject_NewBookingShouldBePossible()
    {
        var eventItem = await CreateTestEventAsync(totalSeats: 1);

        using var scope = _serviceProvider.CreateScope();
        var bookingService = scope.ServiceProvider.GetRequiredService<IBookingService>();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var booking = await bookingService.CreateBookingAsync(eventItem.Id);
        booking.Reject();

        var dbEvent = await context.Events.FindAsync(eventItem.Id);
        dbEvent?.ReleaseSeats(1);
        await context.SaveChangesAsync();

        var newBooking = await bookingService.CreateBookingAsync(eventItem.Id);
        newBooking.Should().NotBeNull();
        newBooking.Status.Should().Be(BookingStatus.Pending);
    }

    #endregion

    #region Тесты на конкурентность (Concurrency)

    [Fact]
    public async Task Concurrency_ShouldPreventOverbooking()
    {
        var eventItem = await CreateTestEventAsync(totalSeats: 5);
        int successCount = 0;
        int failCount = 0;
        var lockObj = new object();

        var tasks = Enumerable.Range(0, 20).Select(async _ =>
        {
            using var scope = _serviceProvider.CreateScope();
            var bookingService = scope.ServiceProvider.GetRequiredService<IBookingService>();
            try
            {
                await bookingService.CreateBookingAsync(eventItem.Id);
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

        using var checkScope = _serviceProvider.CreateScope();
        var context = checkScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var updatedEvent = await context.Events.FindAsync(eventItem.Id);
        updatedEvent!.AvailableSeats.Should().Be(0);
    }

    [Fact]
    public async Task Concurrency_ShouldCreateUniqueIds()
    {
        var eventItem = await CreateTestEventAsync(totalSeats: 10);
        var createdBookings = new List<Booking>();
        var lockObj = new object();

        var tasks = Enumerable.Range(0, 10).Select(async _ =>
        {
            using var scope = _serviceProvider.CreateScope();
            var bookingService = scope.ServiceProvider.GetRequiredService<IBookingService>();
            var booking = await bookingService.CreateBookingAsync(eventItem.Id);
            lock (lockObj) { createdBookings.Add(booking); }
        });

        await Task.WhenAll(tasks);

        createdBookings.Should().HaveCount(10);
        createdBookings.Select(b => b.Id).Distinct().Should().HaveCount(10);
    }

    #endregion
}
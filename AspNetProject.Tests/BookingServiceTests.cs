using AspNetProject.Infrastructure.DataAccess;
using AspNetProject.Domain.Exceptions;
using AspNetProject.Domain.Entities;
using AspNetProject.Application.Interfaces;
using AspNetProject.Application.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using AspNetProject.Infrastructure.Repositories;
using Xunit;

namespace AspNetProject.Tests;

public class BookingServiceTests : IDisposable
{
    private readonly string _dbName;
    private readonly ServiceProvider _serviceProvider;
    private readonly Guid _testUserId = Guid.NewGuid(); // Уже было у тебя, отлично!

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

        services.Configure<AspNetProject.Application.Settings.BookingSettings>(options =>
        {
            options.MaxActiveBookingsPerUser = 10;
        });

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

        var @event = Event.Create("Test Event", "Desc", DateTime.UtcNow.AddHours(1), DateTime.UtcNow.AddHours(2), totalSeats);

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

        var result = await bookingService.CreateBookingAsync(eventItem.Id, _testUserId);

        result.Should().NotBeNull();
        result.Status.Should().Be(BookingStatus.Pending);
        result.EventId.Should().Be(eventItem.Id);
        result.UserId.Should().Be(_testUserId); // Новая проверка
    }

    [Fact]
    public async Task CreateBookingAsync_ShouldThrowKeyNotFoundException_WhenEventDoesNotExist()
    {
        var nonExistentId = Guid.NewGuid();

        using var scope = _serviceProvider.CreateScope();
        var bookingService = scope.ServiceProvider.GetRequiredService<IBookingService>();
        await Assert.ThrowsAsync<KeyNotFoundException>(() => bookingService.CreateBookingAsync(nonExistentId, _testUserId));
    }

    [Fact]
    public async Task GetBookingByIdAsync_ShouldReturnBooking_WhenExists()
    {
        var eventItem = await CreateTestEventAsync(10);

        using var scope = _serviceProvider.CreateScope();
        var bookingService = scope.ServiceProvider.GetRequiredService<IBookingService>();

        var createdBooking = await bookingService.CreateBookingAsync(eventItem.Id, _testUserId);

        var result = await bookingService.GetBookingByIdAsync(createdBooking.Id, _testUserId, Role.User);

        result.Should().NotBeNull();
        result.Id.Should().Be(createdBooking.Id);
    }

    [Fact]
    public async Task GetBookingByIdAsync_ShouldReturnNull_WhenNotExists()
    {
        var nonExistentId = Guid.NewGuid();

        using var scope = _serviceProvider.CreateScope();
        var bookingService = scope.ServiceProvider.GetRequiredService<IBookingService>();

        var result = await bookingService.GetBookingByIdAsync(nonExistentId, _testUserId, Role.User);

        result.Should().BeNull();
    }

    [Fact]
    public async Task CreateBookingAsync_ShouldCreateUniqueIds_ForSameEvent()
    {
        var eventItem = await CreateTestEventAsync(100);

        using var scope = _serviceProvider.CreateScope();
        var bookingService = scope.ServiceProvider.GetRequiredService<IBookingService>();

        var booking1 = await bookingService.CreateBookingAsync(eventItem.Id, _testUserId);
        var booking2 = await bookingService.CreateBookingAsync(eventItem.Id, _testUserId);

        booking1.Id.Should().NotBe(booking2.Id);
    }

    [Fact]
    public async Task Booking_ShouldReflectStatusChange_AfterConfirm()
    {
        var eventItem = await CreateTestEventAsync(10);

        using var scope = _serviceProvider.CreateScope();
        var bookingService = scope.ServiceProvider.GetRequiredService<IBookingService>();

        var booking = await bookingService.CreateBookingAsync(eventItem.Id, _testUserId);

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

        await Assert.ThrowsAsync<KeyNotFoundException>(() => bookingService.CreateBookingAsync(eventItem.Id, _testUserId));
    }

    #endregion

    #region Тесты на места (Seats) — Спринт 4

    [Fact]
    public async Task CreateBookingAsync_ShouldDecreaseAvailableSeats()
    {
        var eventItem = await CreateTestEventAsync(totalSeats: 1);

        using var scope = _serviceProvider.CreateScope();
        var bookingService = scope.ServiceProvider.GetRequiredService<IBookingService>();

        await bookingService.CreateBookingAsync(eventItem.Id, _testUserId);

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

        await bookingService.CreateBookingAsync(eventItem.Id, _testUserId);
        await Assert.ThrowsAsync<NoAvailableSeatsException>(() => bookingService.CreateBookingAsync(eventItem.Id, _testUserId));
    }

    #endregion

    #region Тесты на смену статуса

    [Fact]
    public void Booking_Confirm_ShouldSetStatusAndProcessedAt()
    {
        var eventItem = Event.Create("Test", "Desc", DateTime.UtcNow, DateTime.UtcNow.AddHours(1), 1);

        var booking = Booking.CreatePending(eventItem.Id, Guid.NewGuid());

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

        var booking = Booking.CreatePending(eventItem.Id, Guid.NewGuid());
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

        var booking = await bookingService.CreateBookingAsync(eventItem.Id, _testUserId);
        booking.Reject();

        var dbEvent = await context.Events.FindAsync(eventItem.Id);
        dbEvent?.ReleaseSeats(1);
        await context.SaveChangesAsync();

        var newBooking = await bookingService.CreateBookingAsync(eventItem.Id, _testUserId);
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
                await bookingService.CreateBookingAsync(eventItem.Id, _testUserId);
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

            var booking = await bookingService.CreateBookingAsync(eventItem.Id, _testUserId);
            lock (lockObj) { createdBookings.Add(booking); }
        });

        await Task.WhenAll(tasks);

        createdBookings.Should().HaveCount(10);
        createdBookings.Select(b => b.Id).Distinct().Should().HaveCount(10);
    }

    #endregion

    #region Новые бизнес-правила (Спринт 8)

    [Fact]
    public async Task CreateBookingAsync_ShouldThrowEventAlreadyStartedException_WhenEventHasAlreadyStarted()
    {
        // Arrange
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // Создаем событие, которое началось 1 час назад
        var @event = Event.Create("Прошедшее событие", "Desc", DateTime.UtcNow.AddHours(-1), DateTime.UtcNow.AddHours(1), 10);
        context.Events.Add(@event);
        await context.SaveChangesAsync();

        var bookingService = scope.ServiceProvider.GetRequiredService<IBookingService>();

        // Act & Assert
        await Assert.ThrowsAsync<EventAlreadyStartedException>(() =>
            bookingService.CreateBookingAsync(@event.Id, _testUserId));
    }

    [Fact]
    public async Task CreateBookingAsync_ShouldThrowBookingLimitExceededException_WhenUserHas10ActiveBookings()
    {
        // Arrange
        var eventItem = await CreateTestEventAsync(totalSeats: 20); // Много мест, чтобы не упереться в лимит мест

        using var scope = _serviceProvider.CreateScope();
        var bookingService = scope.ServiceProvider.GetRequiredService<IBookingService>();

        // Создаем 10 активных броней для текущего пользователя
        for (int i = 0; i < 10; i++)
        {
            await bookingService.CreateBookingAsync(eventItem.Id, _testUserId);
        }

        // Act & Assert: 11-я бронь должна выбросить исключение
        await Assert.ThrowsAsync<BookingLimitExceededException>(() =>
            bookingService.CreateBookingAsync(eventItem.Id, _testUserId));
    }

    [Fact]
    public async Task CreateBookingAsync_ShouldAllowDifferentUsersToReachTheirOwnLimitsIndependently()
    {
        // Arrange
        var eventItem = await CreateTestEventAsync(totalSeats: 30);

        using var scope = _serviceProvider.CreateScope();
        var bookingService = scope.ServiceProvider.GetRequiredService<IBookingService>();

        var anotherUserId = Guid.NewGuid();

        // Пользователь 1 делает 10 броней
        for (int i = 0; i < 10; i++)
        {
            await bookingService.CreateBookingAsync(eventItem.Id, _testUserId);
        }

        // Пользователь 2 также должен иметь возможность сделать 10 броней (его лимит независим)
        for (int i = 0; i < 10; i++)
        {
            // Это не должно выбрасывать исключение
            var booking = await bookingService.CreateBookingAsync(eventItem.Id, anotherUserId);
            booking.Should().NotBeNull();
        }

        // А вот 11-я бронь для Пользователя 2 уже должна быть заблокирована
        await Assert.ThrowsAsync<BookingLimitExceededException>(() =>
            bookingService.CreateBookingAsync(eventItem.Id, anotherUserId));
    }

    [Fact]
    public async Task CancelBookingAsync_ShouldThrow_WhenBookingIsAlreadyCancelled()
    {
        // Arrange
        var eventItem = await CreateTestEventAsync(totalSeats: 10);
        using var scope = _serviceProvider.CreateScope();
        var bookingService = scope.ServiceProvider.GetRequiredService<IBookingService>();

        var booking = await bookingService.CreateBookingAsync(eventItem.Id, _testUserId);

        // Отменяем первый раз (успешно)
        await bookingService.CancelBookingAsync(booking.Id, _testUserId, Role.User);

        // Act & Assert: Попытка отменить повторно
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            bookingService.CancelBookingAsync(booking.Id, _testUserId, Role.User));
    }

    #endregion

    #region Тесты отмены брони (Сценарии авторизации и правил)

    [Fact]
    public async Task CancelBookingAsync_ShouldSucceed_WhenOwnerCancelsOwnBooking()
    {
        var eventItem = await CreateTestEventAsync(10);
        using var scope = _serviceProvider.CreateScope();
        var bookingService = scope.ServiceProvider.GetRequiredService<IBookingService>();

        var booking = await bookingService.CreateBookingAsync(eventItem.Id, _testUserId);

        await bookingService.CancelBookingAsync(booking.Id, _testUserId, Role.User);

        var cancelled = await bookingService.GetBookingByIdAsync(booking.Id, _testUserId, Role.User);
        cancelled.Status.Should().Be(BookingStatus.Cancelled);
    }

    [Fact]
    public async Task CancelBookingAsync_ShouldThrowUnauthorizedCancellationException_WhenStrangerTriesToCancel()
    {
        var eventItem = await CreateTestEventAsync(10);
        using var scope = _serviceProvider.CreateScope();
        var bookingService = scope.ServiceProvider.GetRequiredService<IBookingService>();

        var strangerId = Guid.NewGuid();
        var booking = await bookingService.CreateBookingAsync(eventItem.Id, strangerId);

        await Assert.ThrowsAsync<UnauthorizedCancellationException>(() =>
            bookingService.CancelBookingAsync(booking.Id, _testUserId, Role.User));
    }

    [Fact]
    public async Task CancelBookingAsync_ShouldSucceed_WhenAdminCancelsStrangersBooking()
    {
        var eventItem = await CreateTestEventAsync(10);
        using var scope = _serviceProvider.CreateScope();
        var bookingService = scope.ServiceProvider.GetRequiredService<IBookingService>();

        var strangerId = Guid.NewGuid();
        var booking = await bookingService.CreateBookingAsync(eventItem.Id, strangerId);

        await bookingService.CancelBookingAsync(booking.Id, _testUserId, Role.Admin);

        var cancelled = await bookingService.GetBookingByIdAsync(booking.Id, _testUserId, Role.Admin);
        cancelled.Status.Should().Be(BookingStatus.Cancelled);
    }

    [Fact]
    public async Task CancelBookingAsync_ShouldThrowEventAlreadyStartedException_WhenEventAlreadyStarted()
    {
        // Arrange: Создаем событие и бронь
        var eventItem = await CreateTestEventAsync(10);
        using var scope = _serviceProvider.CreateScope();
        var bookingService = scope.ServiceProvider.GetRequiredService<IBookingService>();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var booking = await bookingService.CreateBookingAsync(eventItem.Id, _testUserId);

        // Имитируем, что событие уже началось (меняем дату в БД)
        var dbEvent = await context.Events.FindAsync(eventItem.Id);
        dbEvent!.StartAt = DateTime.UtcNow.AddHours(-1); // Событие началось час назад
        await context.SaveChangesAsync();

        // Act & Assert
        await Assert.ThrowsAsync<EventAlreadyStartedException>(() =>
            bookingService.CancelBookingAsync(booking.Id, _testUserId, Role.User));
    }

    #endregion
}
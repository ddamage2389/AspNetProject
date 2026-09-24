using System.Text.Json;
using AspNetProject.Bookings.Application;
using AspNetProject.Bookings.Domain;
using AspNetProject.Bookings.Infrastructure;
using AspNetProject.Contracts;
using AspNetProject.Events.Domain.Entities;
using AspNetProject.Events.Application.Caching;
using AspNetProject.Events.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace AspNetProject.Sprint9.Tests;

[Collection("Sprint9")]
public sealed class MessagingTests(DatabaseFixture fixture)
{
    private readonly TestCache _cache = new();
    private async Task<Event> CreateEvent(int seats)
    {
        await using var db = fixture.Events();
        var item = Event.Create("Test", "", DateTime.UtcNow.AddDays(1), DateTime.UtcNow.AddDays(2), seats);
        db.Events.Add(item);
        await db.SaveChangesAsync();
        return item;
    }

    private async Task Confirm(BookingConfirmed message)
    {
        await using var db = fixture.Events();
        await new BookingMessageHandler(db, NullLogger<BookingMessageHandler>.Instance, _cache).HandleAsync(message, default);
    }

    private async Task Cancel(BookingConfirmed message)
    {
        await using var db = fixture.Events();
        await new BookingMessageHandler(db, NullLogger<BookingMessageHandler>.Instance, _cache).HandleAsync(
            new BookingCancelled(message.BookingId, message.EventId, message.UserId, message.Seats, DateTime.UtcNow), default);
    }

    private async Task<int> Seats(Guid id)
    {
        await using var db = fixture.Events();
        return (await db.Events.SingleAsync(x => x.Id == id)).AvailableSeats;
    }

    [Fact]
    public async Task Duplicate_confirmation_across_contexts_decrements_only_once()
    {
        var item = await CreateEvent(10);
        var message = new BookingConfirmed(Guid.NewGuid(), item.Id, Guid.NewGuid(), 3, DateTime.UtcNow);
        await Confirm(message);
        await Confirm(message);
        Assert.Equal(7, await Seats(item.Id));
        Assert.Equal(new[] { CacheKeys.Event(item.Id) }, _cache.Removed);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Cancellation_is_idempotent_in_either_delivery_order(bool cancellationFirst)
    {
        var item = await CreateEvent(10);
        var message = new BookingConfirmed(Guid.NewGuid(), item.Id, Guid.NewGuid(), 3, DateTime.UtcNow);
        if (cancellationFirst) await Cancel(message);
        await Confirm(message);
        await Cancel(message);
        await Cancel(message);
        await Confirm(message);
        Assert.Equal(10, await Seats(item.Id));
        Assert.Equal(cancellationFirst ? 0 : 2, _cache.Removed.Count);
        Assert.All(_cache.Removed, key => Assert.Equal(CacheKeys.Event(item.Id), key));
    }

    [Fact]
    public async Task Insufficient_seats_are_skipped_and_cancellation_does_not_invent_seats()
    {
        var item = await CreateEvent(2);
        var first = new BookingConfirmed(Guid.NewGuid(), item.Id, Guid.NewGuid(), 1, DateTime.UtcNow);
        var oversized = first with { BookingId = Guid.NewGuid(), Seats = 3 };
        await Confirm(first);
        await Confirm(oversized);
        await Cancel(oversized);
        await Confirm(oversized);
        Assert.Equal(1, await Seats(item.Id));
    }

    [Fact]
    public async Task Missing_event_is_recorded_as_skipped()
    {
        var message = new BookingConfirmed(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 1, DateTime.UtcNow);
        await Confirm(message);
        await Confirm(message);
        await using var db = fixture.Events();
        Assert.False((await db.BookingReceipts.SingleAsync(x => x.BookingId == message.BookingId)).Applied);
        Assert.Empty(_cache.Removed);
    }

    [Fact]
    public async Task Kafka_invalidates_event_cache_only_after_database_commit()
    {
        var item = await CreateEvent(10);
        var expectedSeats = 8;
        _cache.OnRemove = async key =>
        {
            Assert.Equal(CacheKeys.Event(item.Id), key);
            // A different connection can see the change only after the transaction commits.
            Assert.Equal(expectedSeats, await Seats(item.Id));
        };
        var message = new BookingConfirmed(Guid.NewGuid(), item.Id, Guid.NewGuid(), 2, DateTime.UtcNow);
        await Confirm(message);
        expectedSeats = 10;
        await Cancel(message);
        Assert.Equal(2, _cache.Removed.Count);
    }

    [Fact]
    public async Task Invalid_message_does_not_change_database()
    {
        var item = await CreateEvent(10);
        await Assert.ThrowsAsync<ArgumentException>(() =>
            Confirm(new BookingConfirmed(Guid.NewGuid(), item.Id, Guid.NewGuid(), -2, DateTime.UtcNow)));
        Assert.Equal(10, await Seats(item.Id));
    }

    [Fact]
    public async Task Confirmation_persists_status_and_outbox_together()
    {
        await using var db = fixture.Bookings();
        var repository = new BookingRepository(db);
        var service = new BookingService(repository);
        var booking = await service.CreateAsync(Guid.NewGuid(), Guid.NewGuid(), 2, 5, default);
        await service.ConfirmAsync(booking.Id, default);
        await using var other = fixture.Bookings();
        Assert.Equal(BookingStatus.Confirmed, (await other.Bookings.FindAsync(booking.Id))!.Status);
        var outgoing = await other.Outbox.SingleAsync(x => x.EventId == booking.EventId);
        var contract = JsonSerializer.Deserialize<BookingConfirmed>(outgoing.Payload)!;
        Assert.Equal(booking.Id, contract.BookingId);
        Assert.Equal(2, contract.Seats);
        Assert.Equal(TopicNames.BookingConfirmed, outgoing.Topic);
        Assert.Null(outgoing.PublishedAt);
        await service.ConfirmAsync(booking.Id, default);
        Assert.Equal(1, await other.Outbox.CountAsync(x => x.EventId == booking.EventId));
    }

    [Fact]
    public async Task Failed_outbox_insert_rolls_back_booking_confirmation()
    {
        await using var db = fixture.Bookings();
        var booking = Booking.Create(Guid.NewGuid(), Guid.NewGuid(), 1);
        db.Bookings.Add(booking);
        await db.SaveChangesAsync();
        booking.Confirm();
        db.Outbox.Add(new OutboxMessage { Topic = new string('x', 101), CreatedAt = DateTime.UtcNow });
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        await using var other = fixture.Bookings();
        Assert.Equal(BookingStatus.Pending, (await other.Bookings.FindAsync(booking.Id))!.Status);
    }

    [Fact]
    public async Task Confirm_cancel_race_uses_optimistic_concurrency()
    {
        await using var db = fixture.Bookings();
        var booking = Booking.Create(Guid.NewGuid(), Guid.NewGuid(), 1);
        db.Bookings.Add(booking);
        await db.SaveChangesAsync();
        await using var concurrent = fixture.Bookings();
        var stale = (await concurrent.Bookings.FindAsync(booking.Id))!;
        booking.Cancel(booking.UserId, false);
        await db.SaveChangesAsync();
        stale.Confirm();
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() =>
            new BookingRepository(concurrent).SaveAsync(new BookingConfirmed(
                stale.Id, stale.EventId, stale.UserId, stale.Seats, stale.ProcessedAt!.Value), default));
        await using var other = fixture.Bookings();
        Assert.False(await other.Outbox.AnyAsync(x => x.EventId == booking.EventId));
    }

    [Fact]
    public async Task Stale_event_edit_cannot_overwrite_Kafka_seat_update()
    {
        var item = await CreateEvent(10);
        await using var db = fixture.Events();
        var stale = (await db.Events.FindAsync(item.Id))!;
        await Confirm(new BookingConfirmed(Guid.NewGuid(), item.Id, Guid.NewGuid(), 2, DateTime.UtcNow));
        stale.Update("Changed", "", stale.StartAt, stale.EndAt);
        db.Events.Update(stale);
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => db.SaveChangesAsync());
        Assert.Equal(8, await Seats(item.Id));
    }

    [Fact]
    public async Task Active_booking_limit_is_enforced()
    {
        await using var db = fixture.Bookings();
        var service = new BookingService(new BookingRepository(db));
        var userId = Guid.NewGuid();
        await service.CreateAsync(Guid.NewGuid(), userId, 1, 1, default);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CreateAsync(Guid.NewGuid(), userId, 1, 1, default));
    }

    [Fact]
    public async Task Cancelling_pending_booking_creates_no_confirmation_event()
    {
        await using var db = fixture.Bookings();
        var service = new BookingService(new BookingRepository(db));
        var booking = await service.CreateAsync(Guid.NewGuid(), Guid.NewGuid(), 1, 5, default);
        await service.CancelAsync(booking.Id, booking.UserId, false, default);
        await service.ConfirmAsync(booking.Id, default);
        Assert.Equal(BookingStatus.Cancelled, booking.Status);
        Assert.False(await db.Outbox.AnyAsync(x => x.EventId == booking.EventId));
    }
}

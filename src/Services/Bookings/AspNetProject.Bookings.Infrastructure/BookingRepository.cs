using System.Data;
using System.Text.Json;
using AspNetProject.Bookings.Application;
using AspNetProject.Bookings.Domain;
using AspNetProject.Contracts;
using Microsoft.EntityFrameworkCore;

namespace AspNetProject.Bookings.Infrastructure;

public sealed class BookingRepository(BookingsDbContext db) : IBookingRepository
{
    public Task<Booking?> FindAsync(Guid id, CancellationToken ct) =>
        db.Bookings.SingleOrDefaultAsync(x => x.Id == id, ct);

    public async Task AddAsync(Booking booking, int maxActiveBookings, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var count = await db.Bookings.CountAsync(x => x.UserId == booking.UserId &&
            (x.Status == BookingStatus.Pending || x.Status == BookingStatus.Confirmed), ct);
        if (count >= maxActiveBookings) throw new InvalidOperationException("Active booking limit reached.");
        db.Bookings.Add(booking);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
    }

    public async Task<IReadOnlyList<Guid>> PendingIdsAsync(CancellationToken ct) =>
        await db.Bookings.Where(x => x.Status == BookingStatus.Pending)
            .OrderBy(x => x.CreatedAt).Select(x => x.Id).Take(50).ToListAsync(ct);

    public Task SaveAsync(BookingConfirmed message, CancellationToken ct)
    {
        AddOutbox(TopicNames.BookingConfirmed, message.EventId, message);
        return db.SaveChangesAsync(ct);
    }

    public Task SaveAsync(BookingCancelled? message, CancellationToken ct)
    {
        if (message is not null) AddOutbox(TopicNames.BookingCancelled, message.EventId, message);
        return db.SaveChangesAsync(ct);
    }

    private void AddOutbox<T>(string topic, Guid eventId, T message) =>
        db.Outbox.Add(new OutboxMessage
        {
            Topic = topic, EventId = eventId, Payload = JsonSerializer.Serialize(message), CreatedAt = DateTime.UtcNow
        });
}

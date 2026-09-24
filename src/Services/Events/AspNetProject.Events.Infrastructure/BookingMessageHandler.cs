using System.Data;
using AspNetProject.Contracts;
using AspNetProject.Events.Application;
using AspNetProject.Events.Application.Caching;
using AspNetProject.Events.Infrastructure.DataAccess;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AspNetProject.Events.Infrastructure;

public sealed class BookingMessageHandler(EventsDbContext db, ILogger<BookingMessageHandler> logger, ICache cache) : IBookingMessageHandler
{
    public async Task HandleAsync(BookingConfirmed message, CancellationToken ct)
    {
        Validate(message.BookingId, message.EventId, message.UserId, message.Seats, message.ConfirmedAt);
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        if (await db.BookingReceipts.AnyAsync(x => x.BookingId == message.BookingId, ct))
        {
            await tx.CommitAsync(ct);
            return;
        }

        // Conditional SQL update prevents negative seats under concurrent bookings.
        var changed = await db.Events.Where(x => x.Id == message.EventId && x.AvailableSeats >= message.Seats)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.AvailableSeats, x => x.AvailableSeats - message.Seats), ct);
        db.BookingReceipts.Add(new BookingReceipt
        {
            BookingId = message.BookingId, EventId = message.EventId, Seats = message.Seats, Applied = changed == 1
        });
        if (changed == 0)
            logger.LogWarning("Skipped booking {BookingId}: event {EventId} missing or insufficient seats.",
                message.BookingId, message.EventId);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        if (changed == 1) await cache.RemoveAsync(CacheKeys.Event(message.EventId));
    }

    public async Task HandleAsync(BookingCancelled message, CancellationToken ct)
    {
        Validate(message.BookingId, message.EventId, message.UserId, message.Seats, message.CancelledAt);
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var receipt = await db.BookingReceipts.SingleOrDefaultAsync(x => x.BookingId == message.BookingId, ct);
        var changed = 0;
        if (receipt is null)
        {
            // Different topics may arrive in either order. Later confirmation must not reserve cancelled seats.
            db.BookingReceipts.Add(new BookingReceipt
            {
                BookingId = message.BookingId, EventId = message.EventId, Seats = message.Seats, Cancelled = true
            });
        }
        else if (!receipt.Cancelled)
        {
            if (receipt.EventId != message.EventId || receipt.Seats != message.Seats)
                throw new ArgumentException("Cancellation does not match the original booking.");
            if (receipt.Applied)
                changed = await db.Events.Where(x => x.Id == receipt.EventId)
                    .ExecuteUpdateAsync(s => s.SetProperty(x => x.AvailableSeats, x => x.AvailableSeats + receipt.Seats), ct);
            receipt.Cancelled = true;
        }
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        if (changed == 1) await cache.RemoveAsync(CacheKeys.Event(message.EventId));
    }

    private static void Validate(Guid bookingId, Guid eventId, Guid userId, int seats, DateTime timestamp)
    {
        if (bookingId == Guid.Empty || eventId == Guid.Empty || userId == Guid.Empty || seats <= 0 || timestamp == default)
            throw new ArgumentException("Invalid booking integration event.");
    }
}

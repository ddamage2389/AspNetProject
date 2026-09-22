using AspNetProject.Bookings.Domain;
using AspNetProject.Contracts;

namespace AspNetProject.Bookings.Application;

public sealed class BookingService(IBookingRepository repository)
{
    public async Task<Booking> CreateAsync(Guid eventId, Guid userId, int seats, int maxActiveBookings, CancellationToken ct)
    {
        var booking = Booking.Create(eventId, userId, seats);
        await repository.AddAsync(booking, maxActiveBookings, ct);
        return booking;
    }

    public async Task<Booking?> GetAsync(Guid id, Guid userId, bool isAdmin, CancellationToken ct)
    {
        var booking = await repository.FindAsync(id, ct);
        return booking is not null && (isAdmin || booking.UserId == userId) ? booking : null;
    }

    public async Task ConfirmAsync(Guid id, CancellationToken ct)
    {
        var booking = await repository.FindAsync(id, ct);
        if (booking is null || booking.Status != BookingStatus.Pending) return;
        booking.Confirm();
        await repository.SaveAsync(new BookingConfirmed(
            booking.Id, booking.EventId, booking.UserId, booking.Seats, booking.ProcessedAt!.Value), ct);
    }

    public async Task CancelAsync(Guid id, Guid userId, bool isAdmin, CancellationToken ct)
    {
        var booking = await repository.FindAsync(id, ct) ?? throw new KeyNotFoundException("Booking not found.");
        var confirmed = booking.Cancel(userId, isAdmin);
        await repository.SaveAsync(confirmed ? new BookingCancelled(
            booking.Id, booking.EventId, booking.UserId, booking.Seats, booking.ProcessedAt!.Value) : null, ct);
    }
}

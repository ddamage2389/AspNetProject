using AspNetProject.Bookings.Domain;
using AspNetProject.Contracts;

namespace AspNetProject.Bookings.Application;

public interface IBookingRepository
{
    Task<Booking?> FindAsync(Guid id, CancellationToken ct);
    Task AddAsync(Booking booking, int maxActiveBookings, CancellationToken ct);
    Task<IReadOnlyList<Guid>> PendingIdsAsync(CancellationToken ct);
    // Status and outgoing event must be persisted in one local transaction.
    Task SaveAsync(BookingConfirmed message, CancellationToken ct);
    Task SaveAsync(BookingCancelled? message, CancellationToken ct);
}

public interface IBookingEventPublisher
{
    Task PublishAsync(BookingConfirmed message, CancellationToken ct);
    Task PublishAsync(BookingCancelled message, CancellationToken ct);
}

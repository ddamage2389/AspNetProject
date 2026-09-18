using AspNetProject.Domain.Entities;

namespace AspNetProject.Application.Interfaces;

public interface IBookingService
{
    Task<Booking> CreateBookingAsync(Guid eventId, Guid userId);
    Task<Booking?> GetBookingByIdAsync(Guid bookingId, Guid currentUserId, Role currentUserRole);
    Task CancelBookingAsync(Guid bookingId, Guid currentUserId, Role currentUserRole);
}
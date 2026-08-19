using AspNetProject.Domain.Entities;

namespace AspNetProject.Application.Interfaces;

public interface IBookingService
{
    Task<Booking> CreateBookingAsync(Guid eventId);

    Task<Booking?> GetBookingByIdAsync(Guid bookingId);
}
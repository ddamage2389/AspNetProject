using AspNetProject.Models;

namespace AspNetProject.Services;

public interface IBookingService
{
    /// <summary>
    /// Создаёт бронь для указанного события.
    /// </summary>
    Task<Booking> CreateBookingAsync(Guid eventId);

    /// <summary>
    /// Получает бронь по идентификатору.
    /// </summary>
    Task<Booking?> GetBookingByIdAsync(Guid bookingId);
}
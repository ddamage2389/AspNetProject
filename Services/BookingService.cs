using AspNetProject.DataAccess;
using AspNetProject.Exceptions;
using AspNetProject.Models;
using Microsoft.EntityFrameworkCore;

namespace AspNetProject.Services;

internal sealed class BookingService : IBookingService
{
    private readonly AppDbContext _context;

    // 🔒 static SemaphoreSlim синхронизирует все scoped-экземпляры сервиса
    private static readonly SemaphoreSlim _bookingSemaphore = new(1, 1);

    public BookingService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<Booking> CreateBookingAsync(Guid eventId)
    {
        // Захватываем семафор перед критической секцией
        await _bookingSemaphore.WaitAsync();
        try
        {
            // 1. Получаем событие
            var existingEvent = await _context.Events.FindAsync(eventId);
            if (existingEvent == null)
            {
                throw new KeyNotFoundException($"Событие с ID {eventId} не найдено.");
            }

            // 2. Проверяем и резервируем места
            if (!existingEvent.TryReserveSeats(1))
            {
                throw new NoAvailableSeatsException("No available seats for this event");
            }

            // 3. Создаём бронь
            var booking = Booking.CreatePending(eventId);
            _context.Bookings.Add(booking);

            // 4. Один SaveChangesAsync сохраняет И изменение AvailableSeats у события, И новую бронь
            await _context.SaveChangesAsync();

            return booking;
        }
        finally
        {
            // 🔓 Всегда освобождаем семафор
            _bookingSemaphore.Release();
        }
    }

    public async Task<Booking?> GetBookingByIdAsync(Guid bookingId)
    {
        return await _context.Bookings.FindAsync(bookingId);
    }
}
using AspNetProject.DataAccess;
using AspNetProject.Exceptions;
using AspNetProject.Models;

namespace AspNetProject.Services;

public class BookingService : IBookingService
{
    private readonly IEventService _eventService;
    private readonly InMemoryBookingStore _bookingStore;

    private readonly object _bookingLock = new();

    public BookingService(IEventService eventService, InMemoryBookingStore bookingStore)
    {
        _eventService = eventService;
        _bookingStore = bookingStore;
    }

    public async Task<Booking> CreateBookingAsync(Guid eventId)
    {
        lock (_bookingLock)
        {
            // 1. Получаем событие из хранилища
            var existingEvent = _eventService.GetByIdAsync(eventId).Result;

            if (existingEvent == null)
            {
                throw new KeyNotFoundException($"Событие с ID {eventId} не найдено.");
            }

            // 2. Проверяем и резервируем места атомарно
            if (!existingEvent.TryReserveSeats(1))
            {
                throw new NoAvailableSeatsException("No available seats for this event");
            }

            // 3. Создаём и сохраняем бронь
            var booking = Booking.CreatePending(eventId);
            _bookingStore.Add(booking);

            return booking;
        }
    }

    public Task<Booking?> GetBookingByIdAsync(Guid bookingId)
    {
        var booking = _bookingStore.GetById(bookingId);
        return Task.FromResult(booking);
    }
}
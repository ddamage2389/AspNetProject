using AspNetProject.DataAccess;
using AspNetProject.Models;

namespace AspNetProject.Services;

public class BookingService : IBookingService
{
    private readonly IEventService _eventService;
    private readonly InMemoryBookingStore _bookingStore;

    public BookingService(IEventService eventService, InMemoryBookingStore bookingStore)
    {
        _eventService = eventService;
        _bookingStore = bookingStore;
    }

    public async Task<Booking> CreateBookingAsync(Guid eventId)
    {
        var existingEvent = await _eventService.GetByIdAsync(eventId);
        if (existingEvent == null)
        {
            throw new KeyNotFoundException($"Событие с ID {eventId} не найдено.");
        }

        var booking = Booking.CreatePending(eventId);

        _bookingStore.Add(booking);

        return booking;
    }

    public Task<Booking?> GetBookingByIdAsync(Guid bookingId)
    {
        var booking = _bookingStore.GetById(bookingId);
        return Task.FromResult(booking);
    }
}
using AspNetProject.Application.Interfaces;
using AspNetProject.Domain.Entities;
using AspNetProject.Domain.Exceptions;

namespace AspNetProject.Application.Services;

public sealed class BookingService : IBookingService
{
    private readonly IEventRepository _eventRepository;
    private readonly IBookingRepository _bookingRepository;

    private static readonly SemaphoreSlim _bookingSemaphore = new(1, 1);

    public BookingService(
        IEventRepository eventRepository,
        IBookingRepository bookingRepository)
    {
        _eventRepository = eventRepository;
        _bookingRepository = bookingRepository;
    }

    public async Task<Booking> CreateBookingAsync(Guid eventId)
    {
        await _bookingSemaphore.WaitAsync();

        try
        {
            var existingEvent = await _eventRepository.GetByIdAsync(eventId);

            if (existingEvent is null)
            {
                throw new KeyNotFoundException(
                    $"Событие с ID {eventId} не найдено.");
            }

            if (!existingEvent.TryReserveSeats(1))
            {
                throw new NoAvailableSeatsException(
                    "No available seats for this event");
            }

            var booking = Booking.CreatePending(eventId);

            await _eventRepository.UpdateAsync(existingEvent);
            await _bookingRepository.AddAsync(booking);

            await _bookingRepository.SaveChangesAsync();

            return booking;
        }
        finally
        {
            _bookingSemaphore.Release();
        }
    }

    public async Task<Booking?> GetBookingByIdAsync(Guid bookingId)
    {
        return await _bookingRepository.GetByIdAsync(bookingId);
    }
}
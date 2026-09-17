using AspNetProject.Application.Interfaces;
using AspNetProject.Domain.Entities;
using AspNetProject.Domain.Exceptions;

namespace AspNetProject.Application.Services;

internal sealed class BookingService : IBookingService
{
    private readonly IEventRepository _eventRepository;
    private readonly IBookingRepository _bookingRepository;

    public BookingService(IEventRepository eventRepository, IBookingRepository bookingRepository)
    {
        _eventRepository = eventRepository;
        _bookingRepository = bookingRepository;
    }

    public async Task<Booking> CreateBookingAsync(Guid eventId, Guid userId)
    {
        var existingEvent = await _eventRepository.GetByIdAsync(eventId);
        if (existingEvent == null)
            throw new KeyNotFoundException($"Событие с ID {eventId} не найдено.");

        if (existingEvent.StartAt <= DateTime.UtcNow)
            throw new EventAlreadyStartedException("Нельзя забронировать событие, которое уже началось.");

        var activeBookingsCount = await _bookingRepository.GetActiveBookingsCountAsync(userId);
        if (activeBookingsCount >= 10)
            throw new BookingLimitExceededException("Превышен лимит активных бронирований (максимум 10).");

        if (!existingEvent.TryReserveSeats(1))
            throw new NoAvailableSeatsException("Нет свободных мест для этого события");

        var booking = Booking.CreatePending(eventId, userId);

        await _eventRepository.UpdateAsync(existingEvent);
        await _bookingRepository.AddAsync(booking);
        await _bookingRepository.SaveChangesAsync();

        return booking;
    }

    public async Task<Booking?> GetBookingByIdAsync(Guid bookingId, Guid currentUserId, Role currentUserRole)
    {
        var booking = await _bookingRepository.GetByIdAsync(bookingId);
        if (booking == null) return null;

        if (currentUserRole == Role.User && booking.UserId != currentUserId)
        {
            return null;
        }

        return booking;
    }

    public async Task CancelBookingAsync(Guid bookingId, Guid currentUserId, Role currentUserRole)
    {
        var booking = await _bookingRepository.GetByIdAsync(bookingId);
        if (booking == null)
            throw new KeyNotFoundException("Бронь не найдена.");

        booking.Cancel(currentUserId, currentUserRole);

        await _bookingRepository.UpdateAsync(booking);
        await _bookingRepository.SaveChangesAsync();
    }
}
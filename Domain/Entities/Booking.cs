using AspNetProject.Domain.Exceptions;

namespace AspNetProject.Domain.Entities;

public sealed class Booking
{
    private Booking()
    {
    }

    public Guid Id { get; set; }
    public Guid EventId { get; set; }
    public BookingStatus Status { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ProcessedAt { get; set; }

    internal Event? Event { get; private set; }

    public static Booking CreatePending(Guid eventId)
    {
        return new Booking
        {
            Id = Guid.NewGuid(),
            EventId = eventId,
            Status = BookingStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };
    }

    public void Confirm()
    {
        if (Status != BookingStatus.Pending)
        {
            throw new InvalidBookingStatusException(
                $"Нельзя подтвердить бронь со статусом {Status}. Бронь должна быть в статусе Pending.");
        }

        Status = BookingStatus.Confirmed;
        ProcessedAt = DateTime.UtcNow;
    }

    public void Reject()
    {
        if (Status != BookingStatus.Pending)
        {
            throw new InvalidBookingStatusException(
                $"Нельзя отклонить бронь со статусом {Status}. Бронь должна быть в статусе Pending.");
        }

        Status = BookingStatus.Rejected;
        ProcessedAt = DateTime.UtcNow;
    }
}
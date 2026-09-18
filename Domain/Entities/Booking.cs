using AspNetProject.Domain.Exceptions;

namespace AspNetProject.Domain.Entities;

public sealed class Booking
{
    private Booking() { }

    public Guid Id { get; private set; }
    public Guid EventId { get; private set; }
    public Guid UserId { get; private set; }
    public BookingStatus Status { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? ProcessedAt { get; private set; }

    internal Event? Event { get; private set; }

    public static Booking CreatePending(Guid eventId, Guid userId)
    {
        return new Booking
        {
            Id = Guid.NewGuid(),
            EventId = eventId,
            UserId = userId,
            Status = BookingStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };
    }

    public void Confirm()
    {
        if (Status != BookingStatus.Pending)
            throw new InvalidOperationException($"Нельзя подтвердить бронь со статусом {Status}.");

        Status = BookingStatus.Confirmed;
        ProcessedAt = DateTime.UtcNow;
    }

    public void Reject()
    {
        if (Status != BookingStatus.Pending)
            throw new InvalidOperationException($"Нельзя отклонить бронь со статусом {Status}.");

        Status = BookingStatus.Rejected;
        ProcessedAt = DateTime.UtcNow;
    }

    public void Cancel(Guid currentUserId, Role currentUserRole)
    {
        if (Status == BookingStatus.Cancelled)
            throw new InvalidOperationException("Бронь уже отменена.");

        if (currentUserRole == Role.User && UserId != currentUserId)
        {
            throw new UnauthorizedCancellationException("Вы не можете отменить чужую бронь.");
        }

        Status = BookingStatus.Cancelled;
        ProcessedAt = DateTime.UtcNow;
    }
}
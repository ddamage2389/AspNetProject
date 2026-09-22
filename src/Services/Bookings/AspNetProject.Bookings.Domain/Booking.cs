namespace AspNetProject.Bookings.Domain;

public enum BookingStatus { Pending, Confirmed, Cancelled }

public sealed class Booking
{
    private Booking() { }
    public Guid Id { get; private set; }
    public Guid EventId { get; private set; }
    public Guid UserId { get; private set; }
    public int Seats { get; private set; }
    public BookingStatus Status { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? ProcessedAt { get; private set; }
    public Guid Version { get; private set; }

    public static Booking Create(Guid eventId, Guid userId, int seats)
    {
        if (eventId == Guid.Empty || userId == Guid.Empty)
            throw new ArgumentException("EventId and UserId are required.");
        if (seats <= 0) throw new ArgumentException("Seats must be positive.");
        return new Booking
        {
            Id = Guid.NewGuid(), EventId = eventId, UserId = userId, Seats = seats,
            CreatedAt = DateTime.UtcNow, Version = Guid.NewGuid()
        };
    }

    public void Confirm()
    {
        if (Status != BookingStatus.Pending)
            throw new InvalidOperationException("Only pending bookings can be confirmed.");
        Status = BookingStatus.Confirmed;
        ProcessedAt = DateTime.UtcNow;
        Version = Guid.NewGuid();
    }

    public bool Cancel(Guid userId, bool isAdmin)
    {
        if (!isAdmin && UserId != userId) throw new UnauthorizedAccessException("Not your booking.");
        if (Status == BookingStatus.Cancelled) return false;
        var wasConfirmed = Status == BookingStatus.Confirmed;
        Status = BookingStatus.Cancelled;
        ProcessedAt = DateTime.UtcNow;
        Version = Guid.NewGuid();
        return wasConfirmed;
    }
}

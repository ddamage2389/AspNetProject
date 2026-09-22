namespace AspNetProject.Contracts;

public sealed record BookingCancelled(
    Guid BookingId, Guid EventId, Guid UserId, int Seats, DateTime CancelledAt);

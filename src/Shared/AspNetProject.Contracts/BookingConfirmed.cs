namespace AspNetProject.Contracts;

/// <summary>Version 1 integration event. All timestamps are UTC.</summary>
public sealed record BookingConfirmed(
    Guid BookingId, Guid EventId, Guid UserId, int Seats, DateTime ConfirmedAt);

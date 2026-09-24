using AspNetProject.Events.Domain.Entities;

namespace AspNetProject.Events.Application.Dtos;

// Immutable read model: preserves the HTTP response shape and safely round-trips through JSON.
public sealed record EventDetails(
    Guid Id, string Title, string? Description, DateTime StartAt, DateTime EndAt,
    int TotalSeats, int AvailableSeats)
{
    public static EventDetails From(Event item) => new(
        item.Id, item.Title, item.Description, item.StartAt, item.EndAt, item.TotalSeats, item.AvailableSeats);
}

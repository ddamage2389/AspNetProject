using AspNetProject.Contracts;

namespace AspNetProject.Events.Application;

public interface IBookingMessageHandler
{
    Task HandleAsync(BookingConfirmed message, CancellationToken ct);
    Task HandleAsync(BookingCancelled message, CancellationToken ct);
}

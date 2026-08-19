using AspNetProject.Domain.Entities;

namespace AspNetProject.Application.Interfaces;

public interface IBookingRepository
{
    Task<Booking?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    Task AddAsync(
        Booking booking,
        CancellationToken cancellationToken = default);

    Task<List<Guid>> GetPendingBookingIdsAsync(
        CancellationToken cancellationToken = default);

    Task SaveChangesAsync(
        CancellationToken cancellationToken = default);
}
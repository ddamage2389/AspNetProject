using AspNetProject.Dtos;
using AspNetProject.Models;

namespace AspNetProject.Repositories;

public interface IEventRepository
{
    Task<PaginatedResult<Event>> GetAllAsync(string? title, DateTime? from, DateTime? to, int page, int pageSize, CancellationToken cancellationToken = default);
    Task<Event?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task AddAsync(Event eventItem, CancellationToken cancellationToken = default);
    Task UpdateAsync(Event eventItem, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
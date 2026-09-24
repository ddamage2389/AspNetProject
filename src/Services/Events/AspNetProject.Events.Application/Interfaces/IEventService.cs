using AspNetProject.Events.Application.Dtos;
using AspNetProject.Events.Domain.Entities;

namespace AspNetProject.Events.Application.Interfaces;

public interface IEventService
{
    Task<PaginatedResult<Event>> GetAllAsync(
        string? title = null,
        DateTime? from = null,
        DateTime? to = null,
        int page = 1,
        int pageSize = 10);

    Task<EventDetails?> GetByIdAsync(Guid id);

    Task<IReadOnlyList<EventDetails>> GetTopAsync();

    Task<Event> CreateAsync(Event eventItem);

    Task<Event?> UpdateAsync(Guid id, UpdateEventDto dto);

    Task<bool> DeleteAsync(Guid id);
}

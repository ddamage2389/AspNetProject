
using AspNetProject.Models;
using AspNetApi.Dtos;

namespace AspNetProject.Services;

public interface IEventService
{
    Task<PaginatedResult<Event>> GetAllAsync(
        string? title = null,
        DateTime? from = null,
        DateTime? to = null,
        int page = 1,
        int pageSize = 10);
    Task<Event?> GetByIdAsync(Guid id);
    Task<Event> CreateAsync(Event eventItem);
    Task<Event?> UpdateAsync(Guid id, Event eventItem);
    Task<bool> DeleteAsync(Guid id);
}
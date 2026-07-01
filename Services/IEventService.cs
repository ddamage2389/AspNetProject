
using AspNetProject.Models;

namespace AspNetProject.Services;

public interface IEventService
{
    Task<IEnumerable<Event>> GetAllAsync();
    Task<Event?> GetByIdAsync(Guid id);
    Task<Event> CreateAsync(Event eventItem);
    Task<Event?> UpdateAsync(Guid id, Event eventItem);
    Task<bool> DeleteAsync(Guid id);
}
using AspNetProject.Models;

namespace AspNetProject.Services;

public class EventService : IEventService
{
    private readonly List<Event> _events = new();
    private readonly object _lock = new();

    public Task<IEnumerable<Event>> GetAllAsync()
    {
        lock (_lock) return Task.FromResult<IEnumerable<Event>>(_events.ToList());
    }

    public Task<Event?> GetByIdAsync(Guid id)
    {
        lock (_lock) return Task.FromResult(_events.FirstOrDefault(e => e.Id == id));
    }

    public Task<Event> CreateAsync(Event eventItem)
    {
        lock (_lock)
        {
            eventItem.Id = Guid.NewGuid();
            _events.Add(eventItem);
            return Task.FromResult(eventItem);
        }
    }

    public Task<Event?> UpdateAsync(Guid id, Event updatedEvent)
    {
        lock (_lock)
        {
            var existing = _events.FirstOrDefault(e => e.Id == id);
            if (existing is null) return Task.FromResult<Event?>(null);

            existing.Title = updatedEvent.Title;
            existing.Description = updatedEvent.Description;
            existing.StartAt = updatedEvent.StartAt;
            existing.EndAt = updatedEvent.EndAt;

            return Task.FromResult(existing);
        }
    }

    public Task<bool> DeleteAsync(Guid id)
    {
        lock (_lock)
        {
            var existing = _events.FirstOrDefault(e => e.Id == id);
            if (existing is null) return Task.FromResult(false);

            _events.Remove(existing);
            return Task.FromResult(true);
        }
    }
}
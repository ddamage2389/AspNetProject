using AspNetApi.Dtos;
using AspNetProject.Models;

namespace AspNetProject.Services;

public class EventService : IEventService
{
    private readonly List<Event> _events = new();
    private readonly object _lock = new();

    public Task<PaginatedResult<Event>> GetAllAsync(
    string? title = null,
    DateTime? from = null,
    DateTime? to = null,
    int page = 1,
    int pageSize = 10)
    {
        lock (_lock)
        {
            IEnumerable<Event> query = _events;

            if (!string.IsNullOrWhiteSpace(title))
            {
                query = query.Where(e => e.Title.Contains(title, StringComparison.OrdinalIgnoreCase));
            }

            if (from.HasValue)
            {
                query = query.Where(e => e.StartAt >= from.Value);
            }

            if (to.HasValue)
            {
                query = query.Where(e => e.EndAt <= to.Value);
            }

            var totalCount = query.Count();

            var items = query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            var result = new PaginatedResult<Event>
            {
                Items = items,
                TotalCount = totalCount,
                Page = page,
                PageSize = pageSize
            };

            return Task.FromResult(result);
        }
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

            if (updatedEvent.EndAt <= updatedEvent.StartAt)
            {
                throw new AspNetProject.Exceptions.InvalidEventDatesException(
                    "Поле EndAt должно быть строго позже StartAt");
            }

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
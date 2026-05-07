using AspNetApi.Models;
using EventManager.Services;

namespace AspNetApi.Services;

    public class EventService : IEventService
    {
        private readonly List<Event> _events = new();
        private readonly object _lock = new();

        public Task<IEnumerable<Event>> GetAllAsync()
        {
            lock (_lock)
            {
                return Task.FromResult<IEnumerable<Event>>(_events.ToList());
            }
        }

        public Task<Event?> GetByIdAsync(Guid id)
        {
            lock (_lock)
            {
                var eventItem = _events.FirstOrDefault(e => e.Id == id);
                return Task.FromResult(eventItem);
            }
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

        public Task<Event?> UpdateAsync(Guid id, Event eventItem)
        {
            lock (_lock)
            {
                var existing = _events.FirstOrDefault(e => e.Id == id);
                if (existing is null)
                    return Task.FromResult<Event?>(null);

                existing.Title = eventItem.Title;
                existing.Description = eventItem.Description;
                existing.StartAt = eventItem.StartAt;
                existing.EndAt = eventItem.EndAt;

                return Task.FromResult(existing);
            }
        }

        public Task<bool> DeleteAsync(Guid id)
        {
            lock (_lock)
            {
                var existing = _events.FirstOrDefault(e => e.Id == id);
                if (existing is null)
                    return Task.FromResult(false);

                _events.Remove(existing);
                return Task.FromResult(true);
            }
        }
    }


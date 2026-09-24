using AspNetProject.Events.Application.Dtos;
using AspNetProject.Events.Application.Caching;
using AspNetProject.Events.Application.Interfaces;
using AspNetProject.Events.Domain.Entities;

namespace AspNetProject.Events.Application.Services;

public sealed class EventService : IEventService
{
    private readonly IEventRepository _eventRepository;
    private readonly ICache _cache;
    private readonly CacheSettings _cacheSettings;

    public EventService(IEventRepository eventRepository, ICache cache, CacheSettings cacheSettings)
    {
        _eventRepository = eventRepository;
        _cache = cache;
        _cacheSettings = cacheSettings;
    }

    public async Task<PaginatedResult<Event>> GetAllAsync(
        string? title = null,
        DateTime? from = null,
        DateTime? to = null,
        int page = 1,
        int pageSize = 10)
    {
        return await _eventRepository.GetAllAsync(
            title,
            from,
            to,
            page,
            pageSize);
    }

    public async Task<EventDetails?> GetByIdAsync(Guid id)
    {
        var key = CacheKeys.Event(id);
        var cached = await _cache.GetAsync<EventDetails>(key);
        if (cached is not null) return cached;

        var item = await _eventRepository.GetByIdAsync(id);
        if (item is null) return null;

        var result = EventDetails.From(item);
        await _cache.SetAsync(key, result, TimeSpan.FromSeconds(_cacheSettings.EventTtlSeconds));
        return result;
    }

    public async Task<IReadOnlyList<EventDetails>> GetTopAsync()
    {
        var cached = await _cache.GetAsync<EventDetails[]>(CacheKeys.TopEvents);
        if (cached is not null) return cached;

        var items = await _eventRepository.GetTopAsync();
        var result = items.Select(EventDetails.From).ToArray();
        await _cache.SetAsync(CacheKeys.TopEvents, result, TimeSpan.FromSeconds(_cacheSettings.TopEventsTtlSeconds));
        return result;
    }

    public async Task<Event> CreateAsync(Event eventItem)
    {
        await _eventRepository.AddAsync(eventItem);
        await _eventRepository.SaveChangesAsync();
        await _cache.RemoveAsync(CacheKeys.Event(eventItem.Id));

        return eventItem;
    }

    public async Task<Event?> UpdateAsync(Guid id, UpdateEventDto dto)
    {
        var existing = await _eventRepository.GetByIdAsync(id);

        if (existing is null)
            return null;

        existing.Update(dto.Title, dto.Description, dto.StartAt, dto.EndAt);

        await _eventRepository.UpdateAsync(existing);
        await _eventRepository.SaveChangesAsync();
        await _cache.RemoveAsync(CacheKeys.Event(id));

        return existing;
    }

    public async Task<bool> DeleteAsync(Guid id)
    {
        var existing = await _eventRepository.GetByIdAsync(id);

        if (existing is null)
            return false;

        await _eventRepository.DeleteAsync(id);
        await _eventRepository.SaveChangesAsync();
        await _cache.RemoveAsync(CacheKeys.Event(id));

        return true;
    }
}

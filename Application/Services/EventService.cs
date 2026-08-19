using AspNetProject.Application.Dtos;
using AspNetProject.Application.Interfaces;
using AspNetProject.Domain.Entities;

namespace AspNetProject.Application.Services;

public sealed class EventService : IEventService
{
    private readonly IEventRepository _eventRepository;

    public EventService(IEventRepository eventRepository)
    {
        _eventRepository = eventRepository;
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

    public async Task<Event?> GetByIdAsync(Guid id)
    {
        return await _eventRepository.GetByIdAsync(id);
    }

    public async Task<Event> CreateAsync(Event eventItem)
    {
        await _eventRepository.AddAsync(eventItem);
        await _eventRepository.SaveChangesAsync();

        return eventItem;
    }

    public async Task<Event?> UpdateAsync(Guid id, Event updatedEvent)
    {
        var existing = await _eventRepository.GetByIdAsync(id);

        if (existing is null)
            return null;

        existing.Update(
            updatedEvent.Title,
            updatedEvent.Description,
            updatedEvent.StartAt,
            updatedEvent.EndAt);

        await _eventRepository.UpdateAsync(existing);
        await _eventRepository.SaveChangesAsync();

        return existing;
    }

    public async Task<bool> DeleteAsync(Guid id)
    {
        var existing = await _eventRepository.GetByIdAsync(id);

        if (existing is null)
            return false;

        await _eventRepository.DeleteAsync(id);
        await _eventRepository.SaveChangesAsync();

        return true;
    }
}
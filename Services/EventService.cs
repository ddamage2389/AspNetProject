using AspNetProject.Dtos;
using AspNetProject.Exceptions;
using AspNetProject.Models;
using AspNetProject.Repositories;

namespace AspNetProject.Services;

internal sealed class EventService : IEventService
{
    private readonly IEventRepository _eventRepository;

    public EventService(IEventRepository eventRepository)
    {
        _eventRepository = eventRepository;
    }

    public async Task<PaginatedResult<Event>> GetAllAsync(string? title = null, DateTime? from = null, DateTime? to = null, int page = 1, int pageSize = 10)
    {
        return await _eventRepository.GetAllAsync(title, from, to, page, pageSize);
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
        if (existing is null) return null;

        if (updatedEvent.EndAt <= updatedEvent.StartAt)
        {
            throw new InvalidEventDatesException("Поле EndAt должно быть строго позже StartAt");
        }

        existing.Title = updatedEvent.Title;
        existing.Description = updatedEvent.Description;
        existing.StartAt = updatedEvent.StartAt;
        existing.EndAt = updatedEvent.EndAt;

        await _eventRepository.UpdateAsync(existing);
        await _eventRepository.SaveChangesAsync();
        return existing;
    }

    public async Task<bool> DeleteAsync(Guid id)
    {
        var existing = await _eventRepository.GetByIdAsync(id);
        if (existing is null) return false;

        await _eventRepository.DeleteAsync(id);
        await _eventRepository.SaveChangesAsync();
        return true;
    }
}
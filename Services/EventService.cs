using AspNetProject.DataAccess;
using AspNetProject.Dtos;
using AspNetProject.Models;
using Microsoft.EntityFrameworkCore;

namespace AspNetProject.Services;

internal sealed class EventService : IEventService
{
    private readonly AppDbContext _context;

    public EventService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<PaginatedResult<Event>> GetAllAsync(
        string? title = null,
        DateTime? from = null,
        DateTime? to = null,
        int page = 1,
        int pageSize = 10)
    {
        IQueryable<Event> query = _context.Events;

        if (!string.IsNullOrWhiteSpace(title))
            query = query.Where(e => e.Title.Contains(title, StringComparison.OrdinalIgnoreCase));

        if (from.HasValue)
            query = query.Where(e => e.StartAt >= from.Value);

        if (to.HasValue)
            query = query.Where(e => e.EndAt <= to.Value);

        var totalCount = await query.CountAsync();

        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return new PaginatedResult<Event>
        {
            Items = items,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<Event?> GetByIdAsync(Guid id)
    {
        return await _context.Events.FindAsync(id);
    }

    public async Task<Event> CreateAsync(Event eventItem)
    {
        _context.Events.Add(eventItem);
        await _context.SaveChangesAsync();
        return eventItem;
    }

    public async Task<Event?> UpdateAsync(Guid id, Event updatedEvent)
    {
        var existing = await _context.Events.FindAsync(id);
        if (existing is null) return null;

        if (updatedEvent.EndAt <= updatedEvent.StartAt)
        {
            throw new AspNetProject.Exceptions.InvalidEventDatesException(
                "Поле EndAt должно быть строго позже StartAt");
        }

        existing.Title = updatedEvent.Title;
        existing.Description = updatedEvent.Description;
        existing.StartAt = updatedEvent.StartAt;
        existing.EndAt = updatedEvent.EndAt;

        await _context.SaveChangesAsync();
        return existing;
    }

    public async Task<bool> DeleteAsync(Guid id)
    {
        var existing = await _context.Events.FindAsync(id);
        if (existing is null) return false;

        _context.Events.Remove(existing);
        await _context.SaveChangesAsync();
        return true;
    }
}
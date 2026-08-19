using AspNetProject.Application.Dtos;
using AspNetProject.Application.Interfaces;
using AspNetProject.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using AspNetProject.Infrastructure.DataAccess;

namespace AspNetProject.Infrastructure.Repositories;

public sealed class EventRepository : IEventRepository
{
    private readonly AppDbContext _context;

    public EventRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<PaginatedResult<Event>> GetAllAsync(string? title, DateTime? from, DateTime? to, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        IQueryable<Event> query = _context.Events;

        if (!string.IsNullOrWhiteSpace(title))
        {
            var searchTitle = title.ToLower();
            query = query.Where(e => e.Title.ToLower().Contains(searchTitle));
        }

        if (from.HasValue)
            query = query.Where(e => e.StartAt >= from.Value);

        if (to.HasValue)
            query = query.Where(e => e.EndAt <= to.Value);

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PaginatedResult<Event>
        {
            Items = items,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<Event?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Events.FindAsync(new object[] { id }, cancellationToken);
    }

    public Task AddAsync(Event eventItem, CancellationToken cancellationToken = default)
    {
        _context.Events.Add(eventItem);
        return Task.CompletedTask;
    }

    public Task UpdateAsync(Event eventItem, CancellationToken cancellationToken = default)
    {
        _context.Events.Update(eventItem);
        return Task.CompletedTask;
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var existing = await GetByIdAsync(id, cancellationToken);
        if (existing is not null)
        {
            _context.Events.Remove(existing);
        }
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await _context.SaveChangesAsync(cancellationToken);
    }
}
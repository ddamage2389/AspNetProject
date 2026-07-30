using AspNetProject.DataAccess;
using AspNetProject.Models;
using Microsoft.EntityFrameworkCore;

namespace AspNetProject.Repositories;

internal sealed class BookingRepository : IBookingRepository
{
    private readonly AppDbContext _context;

    public BookingRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<Booking?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Bookings.FindAsync(new object[] { id }, cancellationToken);
    }

    public Task AddAsync(Booking booking, CancellationToken cancellationToken = default)
    {
        _context.Bookings.Add(booking);
        return Task.CompletedTask;
    }

    public async Task<List<Guid>> GetPendingBookingIdsAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Bookings
            .Where(b => b.Status == BookingStatus.Pending)
            .Select(b => b.Id)
            .ToListAsync(cancellationToken);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await _context.SaveChangesAsync(cancellationToken);
    }
}
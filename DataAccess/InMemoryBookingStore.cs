using AspNetProject.Models;

namespace AspNetProject.DataAccess;

public class InMemoryBookingStore
{
    private readonly List<Booking> _bookings = new();

    public void Add(Booking booking)
    {
        _bookings.Add(booking);
    }

    public Booking? GetById(Guid bookingId)
    {
        return _bookings.FirstOrDefault(b => b.Id == bookingId);
    }

    public IEnumerable<Booking> GetAll()
    {
        return _bookings.AsEnumerable();
    }

    public IEnumerable<Booking> GetByStatus(BookingStatus status)
    {
        return _bookings.Where(b => b.Status == status);
    }

    public void Update(Booking booking)
    {
        var existing = _bookings.FirstOrDefault(b => b.Id == booking.Id);
        if (existing != null)
        {
            var index = _bookings.IndexOf(existing);
            _bookings[index] = booking;
        }
    }
}
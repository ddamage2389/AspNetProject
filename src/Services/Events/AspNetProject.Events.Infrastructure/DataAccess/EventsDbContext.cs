using AspNetProject.Events.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace AspNetProject.Events.Infrastructure.DataAccess;

// Durable inbox, including tombstones for cancellations received before confirmations.
public sealed class BookingReceipt
{
    public Guid BookingId { get; set; }
    public Guid EventId { get; set; }
    public int Seats { get; set; }
    public bool Applied { get; set; }
    public bool Cancelled { get; set; }
}

public sealed class EventsDbContext(DbContextOptions<EventsDbContext> options) : DbContext(options)
{
    public DbSet<Event> Events => Set<Event>();
    public DbSet<BookingReceipt> BookingReceipts => Set<BookingReceipt>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var item = modelBuilder.Entity<Event>();
        item.ToTable("events", t =>
        {
            t.HasCheckConstraint("CK_events_seats", "\"AvailableSeats\" >= 0 AND \"AvailableSeats\" <= \"TotalSeats\" AND \"TotalSeats\" > 0");
            t.HasCheckConstraint("CK_events_dates", "\"EndAt\" > \"StartAt\"");
        });
        item.HasKey(x => x.Id);
        item.Property(x => x.Id).ValueGeneratedNever();
        item.Property(x => x.Title).HasMaxLength(200).IsRequired();
        item.Property(x => x.Description).HasMaxLength(2000);
        // A concurrent Kafka update must never be overwritten by CRUD with a stale seat count.
        item.Property(x => x.AvailableSeats).IsConcurrencyToken();
        var receipt = modelBuilder.Entity<BookingReceipt>();
        receipt.ToTable("booking_receipts");
        receipt.HasKey(x => x.BookingId);
        receipt.Property(x => x.BookingId).ValueGeneratedNever();
        // No foreign key: retain deduplication records after an event is deleted.
    }
}

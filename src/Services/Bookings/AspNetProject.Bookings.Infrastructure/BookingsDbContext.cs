using AspNetProject.Bookings.Domain;
using Microsoft.EntityFrameworkCore;

namespace AspNetProject.Bookings.Infrastructure;

public sealed class OutboxMessage
{
    public long Id { get; set; }
    public string Topic { get; set; } = "";
    public Guid EventId { get; set; }
    public string Payload { get; set; } = "";
    public DateTime CreatedAt { get; set; }
    public DateTime? PublishedAt { get; set; }
}

public sealed class BookingsDbContext(DbContextOptions<BookingsDbContext> options) : DbContext(options)
{
    public DbSet<Booking> Bookings => Set<Booking>();
    public DbSet<OutboxMessage> Outbox => Set<OutboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var booking = modelBuilder.Entity<Booking>();
        booking.ToTable("bookings", t => t.HasCheckConstraint("CK_bookings_seats", "\"Seats\" > 0"));
        booking.HasKey(x => x.Id);
        booking.Property(x => x.Id).ValueGeneratedNever();
        booking.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
        booking.Property(x => x.Version).IsConcurrencyToken();
        booking.HasIndex(x => new { x.UserId, x.Status });
        booking.HasIndex(x => x.Status);
        var outbox = modelBuilder.Entity<OutboxMessage>();
        outbox.ToTable("outbox");
        outbox.HasKey(x => x.Id);
        outbox.Property(x => x.Topic).HasMaxLength(100);
        outbox.HasIndex(x => new { x.PublishedAt, x.Id });
    }
}

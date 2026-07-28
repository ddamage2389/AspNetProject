using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using AspNetProject.Models;

namespace AspNetProject.DataAccess.Configurations;

internal sealed class EventConfiguration : IEntityTypeConfiguration<Event>
{
    public void Configure(EntityTypeBuilder<Event> builder)
    {
        // имя таблицы
        builder.ToTable("events");

        // первичный ключ (генерируется в коде)
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedNever();

        // ограничения для свойств
        builder.Property(e => e.Title)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(e => e.Description)
            .HasMaxLength(2000);

        builder.Property(e => e.StartAt)
            .IsRequired();

        builder.Property(e => e.EndAt)
            .IsRequired();

        builder.Property(e => e.TotalSeats)
            .IsRequired();

        builder.Property(e => e.AvailableSeats)
            .IsRequired();

        // один–ко–многим
        builder.HasMany(e => e.Bookings)
            .WithOne(b => b.Event)
            .HasForeignKey(b => b.EventId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
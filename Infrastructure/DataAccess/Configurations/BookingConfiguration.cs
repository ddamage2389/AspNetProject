using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using AspNetProject.Domain.Entities;

namespace AspNetProject.Infrastructure.DataAccess.Configurations;

internal sealed class BookingConfiguration : IEntityTypeConfiguration<Booking>
{
    public void Configure(EntityTypeBuilder<Booking> builder)
    {
        // имя таблицы
        builder.ToTable("bookings");

        // первичный ключ
        builder.HasKey(b => b.Id);
        builder.Property(b => b.Id).ValueGeneratedNever();

        //  enum Status как строка в БД
        builder.Property(b => b.Status)
            .HasConversion<string>()
            .IsRequired();

        builder.Property(b => b.EventId)
            .IsRequired();

        builder.Property(b => b.CreatedAt)
            .IsRequired();

        builder.Property(b => b.ProcessedAt);

        // связь с Event
        builder.HasOne(b => b.Event)
            .WithMany(e => e.Bookings)
            .HasForeignKey(b => b.EventId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
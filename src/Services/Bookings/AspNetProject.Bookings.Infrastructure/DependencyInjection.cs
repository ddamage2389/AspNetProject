using AspNetProject.Bookings.Application;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AspNetProject.Bookings.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddBookings(this IServiceCollection services, IConfiguration config)
    {
        services.AddDbContext<BookingsDbContext>(o => o.UseNpgsql(config.GetConnectionString("Database")));
        services.AddScoped<IBookingRepository, BookingRepository>();
        services.AddScoped<BookingService>();
        services.AddSingleton<IBookingEventPublisher, KafkaBookingEventPublisher>();
        if (config.GetValue("Workers:Enabled", true))
        {
            services.AddHostedService<BookingProcessingWorker>();
            services.AddHostedService<OutboxWorker>();
        }
        return services;
    }
}

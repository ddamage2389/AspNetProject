using AspNetProject.Events.Application;
using AspNetProject.Events.Application.Interfaces;
using AspNetProject.Events.Application.Services;
using AspNetProject.Events.Infrastructure.DataAccess;
using AspNetProject.Events.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace AspNetProject.Events.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddEvents(this IServiceCollection services, IConfiguration config)
    {
        services.AddDbContext<EventsDbContext>(o => o.UseNpgsql(config.GetConnectionString("Database")));
        services.AddScoped<IEventRepository, EventRepository>();
        services.AddScoped<IEventService, EventService>();
        services.AddScoped<IBookingMessageHandler, BookingMessageHandler>();
        if (config.GetValue("Workers:Enabled", true))
        {
            services.AddSingleton<KafkaTopicInitializer>();
            services.AddSingleton<IHostedService>(sp => sp.GetRequiredService<KafkaTopicInitializer>());
            services.AddHostedService<BookingEventsConsumer>();
        }
        return services;
    }
}

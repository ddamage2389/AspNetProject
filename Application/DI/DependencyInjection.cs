using AspNetProject.Application.Interfaces;
using AspNetProject.Application.Services;
using Microsoft.Extensions.DependencyInjection;

namespace AspNetProject.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplicationServices(
        this IServiceCollection services)
    {
        services.AddScoped<IEventService, EventService>();
        services.AddScoped<IBookingService, BookingService>();

        services.AddHostedService<BookingProcessingBackgroundService>();

        return services;
    }
}
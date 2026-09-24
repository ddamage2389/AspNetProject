using AspNetProject.Events.Application;
using AspNetProject.Events.Application.Caching;
using AspNetProject.Events.Application.Interfaces;
using AspNetProject.Events.Application.Services;
using AspNetProject.Events.Infrastructure.DataAccess;
using AspNetProject.Events.Infrastructure.Repositories;
using AspNetProject.Events.Infrastructure.Caching;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace AspNetProject.Events.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddEvents(this IServiceCollection services, IConfiguration config)
    {
        services.AddDbContext<EventsDbContext>(o => o.UseNpgsql(config.GetConnectionString("Database")));
        services.AddScoped<IEventRepository, EventRepository>();
        services.AddScoped<IEventService, EventService>();
        var cacheSettings = config.GetSection("Cache").Get<CacheSettings>() ?? new CacheSettings();
        if (cacheSettings.EventTtlSeconds <= 0 || cacheSettings.TopEventsTtlSeconds <= 0)
            throw new InvalidOperationException("Cache TTL values must be positive.");
        services.AddSingleton(cacheSettings);
        services.AddSingleton<IConnectionMultiplexer>(sp =>
        {
            var options = ConfigurationOptions.Parse(config["Redis:ConnectionString"] ?? "localhost:6379");
            // Redis is optional: reconnect in the background and fail commands quickly while offline.
            options.AbortOnConnectFail = false;
            options.ConnectRetry = 0;
            options.ConnectTimeout = config.GetValue("Redis:ConnectTimeoutMilliseconds", 1000);
            options.AsyncTimeout = config.GetValue("Redis:OperationTimeoutMilliseconds", 500);
            options.BacklogPolicy = BacklogPolicy.FailFast;
            var logger = sp.GetRequiredService<ILogger<RedisCache>>();
            var connection = ConnectionMultiplexer.Connect(options);
            connection.ConnectionFailed += (_, e) => logger.LogWarning(e.Exception, "Redis connection unavailable.");
            connection.ConnectionRestored += (_, _) => logger.LogInformation("Redis connection restored.");
            if (!connection.IsConnected) logger.LogWarning("Redis unavailable at startup; continuing without cache.");
            return connection;
        });
        services.AddSingleton<ICache, RedisCache>();
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

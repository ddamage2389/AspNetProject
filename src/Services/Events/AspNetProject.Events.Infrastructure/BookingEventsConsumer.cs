using System.Text.Json;
using AspNetProject.Contracts;
using AspNetProject.Events.Application;
using Confluent.Kafka;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AspNetProject.Events.Infrastructure;

public sealed class BookingEventsConsumer(
    IServiceScopeFactory scopes, IConfiguration config, KafkaTopicInitializer topics,
    ILogger<BookingEventsConsumer> logger) : BackgroundService
{
    // Consume is blocking. Run the loop off the application startup thread.
    protected override Task ExecuteAsync(CancellationToken stoppingToken) =>
        Task.Run(() => ConsumeAsync(stoppingToken), stoppingToken);

    private async Task ConsumeAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                if (!await topics.EnsureCreatedAsync(ct))
                {
                    await Task.Delay(TimeSpan.FromSeconds(5), ct);
                    continue;
                }
                using var consumer = new ConsumerBuilder<string, string>(new ConsumerConfig
                {
                    BootstrapServers = config["Kafka:BootstrapServers"],
                    GroupId = config["Kafka:ConsumerGroup"] ?? "events-service",
                    AutoOffsetReset = AutoOffsetReset.Earliest,
                    EnableAutoCommit = false,
                    EnableAutoOffsetStore = false,
                    AllowAutoCreateTopics = false
                }).Build();
                try
                {
                    consumer.Subscribe(new[] { TopicNames.BookingConfirmed, TopicNames.BookingCancelled });
                    while (!ct.IsCancellationRequested)
                    {
                        var record = consumer.Consume(ct);
                        try
                        {
                            using var scope = scopes.CreateScope();
                            var handler = scope.ServiceProvider.GetRequiredService<IBookingMessageHandler>();
                            if (record.Topic == TopicNames.BookingConfirmed)
                                await handler.HandleAsync(
                                    JsonSerializer.Deserialize<BookingConfirmed>(record.Message.Value)
                                        ?? throw new JsonException("Null confirmation."), ct);
                            else
                                await handler.HandleAsync(
                                    JsonSerializer.Deserialize<BookingCancelled>(record.Message.Value)
                                        ?? throw new JsonException("Null cancellation."), ct);
                        }
                        catch (Exception ex) when (ex is JsonException or ArgumentException)
                        {
                            logger.LogWarning(ex, "Skipping malformed message at {Position}.", record.TopicPartitionOffset);
                        }
                        // Only after DB commit (or explicit poison-message skip).
                        consumer.Commit(record);
                    }
                }
                finally { consumer.Close(); }
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { break; }
            catch (Exception ex)
            {
                // Recreate the consumer: restart at committed offsets, never advance past a DB failure.
                logger.LogError(ex, "Consumer failed; restarting from committed offsets.");
                try { await Task.Delay(TimeSpan.FromSeconds(3), ct); }
                catch (OperationCanceledException) when (ct.IsCancellationRequested) { break; }
            }
        }
    }
}

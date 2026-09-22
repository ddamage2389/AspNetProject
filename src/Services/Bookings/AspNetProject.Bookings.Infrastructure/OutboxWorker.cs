using System.Text.Json;
using AspNetProject.Bookings.Application;
using AspNetProject.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AspNetProject.Bookings.Infrastructure;

public sealed class OutboxWorker(
    IServiceScopeFactory scopes, IBookingEventPublisher publisher, ILogger<OutboxWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopes.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<BookingsDbContext>();
                // A row lock prevents two dispatchers from concurrently publishing the same row.
                await using var tx = await db.Database.BeginTransactionAsync(stoppingToken);
                var messages = await db.Outbox.FromSqlRaw(
                    """SELECT * FROM outbox WHERE "PublishedAt" IS NULL ORDER BY "Id" LIMIT 1 FOR UPDATE""")
                    .ToListAsync(stoppingToken);
                if (messages.Count > 0)
                {
                    var message = messages[0];
                    // The booking and the outbox row were committed before this worker can read them.
                    switch (message.Topic)
                    {
                        case TopicNames.BookingConfirmed:
                            await publisher.PublishAsync(JsonSerializer.Deserialize<BookingConfirmed>(message.Payload)
                                ?? throw new JsonException("Null outbox confirmation."), stoppingToken);
                            break;
                        case TopicNames.BookingCancelled:
                            await publisher.PublishAsync(JsonSerializer.Deserialize<BookingCancelled>(message.Payload)
                                ?? throw new JsonException("Null outbox cancellation."), stoppingToken);
                            break;
                        default:
                            throw new InvalidOperationException($"Unknown outbox topic: {message.Topic}");
                    }
                    message.PublishedAt = DateTime.UtcNow;
                    await db.SaveChangesAsync(stoppingToken);
                    await tx.CommitAsync(stoppingToken);
                    continue;
                }
                await tx.CommitAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex) { logger.LogError(ex, "Outbox delivery failed; message remains pending for retry."); }
            try { await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
        }
    }
}

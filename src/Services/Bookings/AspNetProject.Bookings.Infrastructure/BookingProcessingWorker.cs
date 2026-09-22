using AspNetProject.Bookings.Application;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AspNetProject.Bookings.Infrastructure;

public sealed class BookingProcessingWorker(IServiceScopeFactory scopes, ILogger<BookingProcessingWorker> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var batchScope = scopes.CreateScope();
                var ids = await batchScope.ServiceProvider.GetRequiredService<IBookingRepository>().PendingIdsAsync(stoppingToken);
                foreach (var id in ids)
                {
                    using var scope = scopes.CreateScope();
                    await scope.ServiceProvider.GetRequiredService<BookingService>().ConfirmAsync(id, stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex) { logger.LogError(ex, "Confirmation failed; pending bookings will be retried."); }
            try { await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
        }
    }
}

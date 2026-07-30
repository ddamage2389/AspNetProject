using AspNetProject.Models;
using AspNetProject.Repositories;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AspNetProject.Services;

public class BookingProcessingBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<BookingProcessingBackgroundService> _logger;

    public BookingProcessingBackgroundService(IServiceScopeFactory scopeFactory, ILogger<BookingProcessingBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Фоновый сервис обработки бронирований запущен.");

        while (!stoppingToken.IsCancellationRequested)
        {
            List<Guid> pendingBookingIds;

            using (var scope = _scopeFactory.CreateScope())
            {
                var bookingRepository = scope.ServiceProvider.GetRequiredService<IBookingRepository>();
                pendingBookingIds = await bookingRepository.GetPendingBookingIdsAsync(stoppingToken);
            }

            if (pendingBookingIds.Any())
            {
                _logger.LogInformation("Найдено {Count} броней для обработки.", pendingBookingIds.Count);
                var tasks = pendingBookingIds.Select(id => ProcessBookingAsync(id, stoppingToken));
                await Task.WhenAll(tasks);
            }

            await Task.Delay(5000, stoppingToken);
        }
    }

    private async Task ProcessBookingAsync(Guid bookingId, CancellationToken stoppingToken)
    {
        try
        {
            await Task.Delay(2000, stoppingToken);

            using var scope = _scopeFactory.CreateScope();
            var bookingRepository = scope.ServiceProvider.GetRequiredService<IBookingRepository>();
            var eventRepository = scope.ServiceProvider.GetRequiredService<IEventRepository>();

            var booking = await bookingRepository.GetByIdAsync(bookingId, stoppingToken);
            if (booking == null) return;

            var eventItem = await eventRepository.GetByIdAsync(booking.EventId, stoppingToken);

            if (eventItem == null)
            {
                booking.Reject();
                await bookingRepository.SaveChangesAsync(stoppingToken);
                _logger.LogWarning("Бронь {BookingId} отклонена: событие не найдено.", bookingId);
            }
            else
            {
                booking.Confirm();
                await bookingRepository.SaveChangesAsync(stoppingToken);
                _logger.LogInformation("Бронь {BookingId} успешно подтверждена.", bookingId);
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при обработке брони {BookingId}", bookingId);

            using var errorScope = _scopeFactory.CreateScope();
            var bookingRepository = errorScope.ServiceProvider.GetRequiredService<IBookingRepository>();
            var eventRepository = errorScope.ServiceProvider.GetRequiredService<IEventRepository>();

            var booking = await bookingRepository.GetByIdAsync(bookingId, stoppingToken);
            if (booking != null)
            {
                booking.Reject();
                var eventItem = await eventRepository.GetByIdAsync(booking.EventId, stoppingToken);
                eventItem?.ReleaseSeats();

                await eventRepository.UpdateAsync(eventItem);
                await bookingRepository.SaveChangesAsync(stoppingToken);
            }
        }
    }
}
using AspNetProject.DataAccess;
using AspNetProject.Models;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AspNetProject.Services;

public class BookingProcessingBackgroundService : BackgroundService
{
    private readonly InMemoryBookingStore _bookingStore;
    private readonly ILogger<BookingProcessingBackgroundService> _logger;

    public BookingProcessingBackgroundService(
        InMemoryBookingStore bookingStore,
        ILogger<BookingProcessingBackgroundService> logger)
    {
        _bookingStore = bookingStore;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Фоновый сервис обработки бронирований запущен.");

        while (!stoppingToken.IsCancellationRequested)
        {
            // 1. Находим все брони со статусом Pending
            var pendingBookings = _bookingStore.GetByStatus(BookingStatus.Pending).ToList();

            foreach (var booking in pendingBookings)
            {
                _logger.LogInformation("Начата обработка брони {BookingId}...", booking.Id);

                try
                {
                    // 2. Имитируем обращение к внешней системе (задержка 2 секунды)
                    await Task.Delay(2000, stoppingToken);

                    // 3. После "успешной" обработки меняем статус на Confirmed
                    booking.Status = BookingStatus.Confirmed;
                    booking.ProcessedAt = DateTime.UtcNow;

                    // 4. Сохраняем изменения в хранилище
                    _bookingStore.Update(booking);

                    _logger.LogInformation("Бронь {BookingId} успешно подтверждена.", booking.Id);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }

            // 5. Ждем 20 секунд перед следующей проверкой хранилища
            await Task.Delay(20000, stoppingToken);
        }
    }
}
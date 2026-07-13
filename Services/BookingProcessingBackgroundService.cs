using AspNetProject.DataAccess;
using AspNetProject.Models;
using AspNetProject.Services;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AspNetProject.Services;

public class BookingProcessingBackgroundService : BackgroundService
{
    private readonly InMemoryBookingStore _bookingStore;
    private readonly IEventService _eventService;
    private readonly ILogger<BookingProcessingBackgroundService> _logger;

    private readonly SemaphoreSlim _processingSemaphore = new(1, 1);

    public BookingProcessingBackgroundService(
        InMemoryBookingStore bookingStore,
        IEventService eventService,
        ILogger<BookingProcessingBackgroundService> logger)
    {
        _bookingStore = bookingStore;
        _eventService = eventService;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Фоновый сервис обработки бронирований запущен.");

        while (!stoppingToken.IsCancellationRequested)
        {
            // 1. Получаем список броней со статусом Pending
            // .ToList() создает копию списка, чтобы избежать ошибок при изменении коллекции
            var pendingBookings = _bookingStore.GetByStatus(BookingStatus.Pending).ToList();

            if (pendingBookings.Any())
            {
                _logger.LogInformation("Найдено {Count} броней для обработки.", pendingBookings.Count);

                // 2. ЗАПУСКАЕМ ОБРАБОТКУ ПАРАЛЛЕЛЬНО
                // Создаем задачу для каждой брони
                var tasks = pendingBookings.Select(booking => ProcessBookingAsync(booking, stoppingToken));

                // Ждем завершения задач
                await Task.WhenAll(tasks);
            }

            // 3. Ждем 5 секунд перед следующей проверкой (Polling Interval)
            await Task.Delay(5000, stoppingToken);
        }
    }

    // 🔹 Логика обработки брони
    private async Task ProcessBookingAsync(Booking booking, CancellationToken stoppingToken)
    {
        try
        {
            // Имитация внешней операции (2 секунды)
            await Task.Delay(2000, stoppingToken);

            await _processingSemaphore.WaitAsync(stoppingToken);

            try
            {
                // Проверяем, существует ли событие
                var @event = await _eventService.GetByIdAsync(booking.EventId);

                if (@event == null)
                {
                    // События нет
                    _logger.LogWarning("Бронь {BookingId} отклонена: событие {EventId} не найдено.", booking.Id, booking.EventId);
                    booking.Reject();
                    _bookingStore.Update(booking);
                }
                else
                {
                    // Событие есть
                    booking.Confirm();
                    _bookingStore.Update(booking);
                    _logger.LogInformation("Бронь {BookingId} успешно подтверждена.", booking.Id);
                }

            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при обработке брони {BookingId}", booking.Id);

                booking.Reject();

                // Пытаемся вернуть места, если событие доступно
                try
                {
                    var @event = await _eventService.GetByIdAsync(booking.EventId);
                    if (@event != null)
                    {
                        @event.ReleaseSeats();
                        // Сохраняем изменения в событии (возврат мест)
                        await _eventService.UpdateAsync(@event.Id, @event);
                    }
                }
                catch { /* Игнорируем ошибки при возврате мест */ }

                // Сохраняем отклоненную бронь
                _bookingStore.Update(booking);
            }
            finally
            {
                _processingSemaphore.Release();
            }
        }
        catch (OperationCanceledException)
        {

        }
    }
}
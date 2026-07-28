using AspNetProject.DataAccess;
using AspNetProject.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AspNetProject.Services;

public class BookingProcessingBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<BookingProcessingBackgroundService> _logger;

    public BookingProcessingBackgroundService(
        IServiceScopeFactory scopeFactory,
        ILogger<BookingProcessingBackgroundService> logger)
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

            // 1. Создаём scope, получаем DbContext и забираем ID pending-бронирований
            using (var scope = _scopeFactory.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                pendingBookingIds = await db.Bookings
                    .Where(b => b.Status == BookingStatus.Pending)
                    .Select(b => b.Id)
                    .ToListAsync(stoppingToken);
            } // ✅ Scope закрывается, DbContext утилизируется

            if (pendingBookingIds.Any())
            {
                _logger.LogInformation("Найдено {Count} броней для обработки.", pendingBookingIds.Count);

                // 2. Запускаем обработку параллельно
                var tasks = pendingBookingIds.Select(id => ProcessBookingAsync(id, stoppingToken));
                await Task.WhenAll(tasks);
            }

            // 3. Пауза перед следующей проверкой
            await Task.Delay(5000, stoppingToken);
        }
    }

    private async Task ProcessBookingAsync(Guid bookingId, CancellationToken stoppingToken)
    {
        try
        {
            // ⏳ Имитация внешней операции (выполняется ДО создания scope → параллельно для всех задач)
            await Task.Delay(2000, stoppingToken);

            // 📦 Каждая задача получает свой изолированный DbContext
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var booking = await db.Bookings.FindAsync(bookingId);
            if (booking == null) return; // Уже обработана или удалена

            var eventItem = await db.Events.FindAsync(booking.EventId);

            if (eventItem == null)
            {
                booking.Reject();
                await db.SaveChangesAsync(stoppingToken);
                _logger.LogWarning("Бронь {BookingId} отклонена: событие не найдено.", bookingId);
            }
            else
            {
                booking.Confirm();
                await db.SaveChangesAsync(stoppingToken);
                _logger.LogInformation("Бронь {BookingId} успешно подтверждена.", bookingId);
            }
        }
        catch (OperationCanceledException)
        {
            // Нормальное завершение при остановке приложения
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при обработке брони {BookingId}", bookingId);

            // 🚨 При ошибке отклоняем бронь и возвращаем места
            using var errorScope = _scopeFactory.CreateScope();
            var db = errorScope.ServiceProvider.GetRequiredService<AppDbContext>();

            var booking = await db.Bookings.FindAsync(bookingId);
            if (booking != null)
            {
                booking.Reject();
                var eventItem = await db.Events.FindAsync(booking.EventId);
                eventItem?.ReleaseSeats();
                await db.SaveChangesAsync(stoppingToken);
            }
        }
    }
}
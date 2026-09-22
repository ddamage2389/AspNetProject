using AspNetProject.Events.Domain.Exceptions;

namespace AspNetProject.Events.Domain.Entities;

public sealed class Event
{
    private Event()
    {
        Title = string.Empty; // Инициализация required поля
    }

    public Guid Id { get; set; }
    public required string Title { get; set; }
    public string? Description { get; set; }
    public DateTime StartAt { get; set; }
    public DateTime EndAt { get; set; }

    public int TotalSeats { get; private set; }
    public int AvailableSeats { get; private set; }


    /// <summary>
    /// Фабричный метод с валидацией
    /// </summary>
    public static Event Create(string title, string description, DateTime startAt, DateTime endAt, int totalSeats)
    {
        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("Title is required", nameof(title));

        if (startAt >= endAt)
            throw new InvalidEventDatesException("EndAt must be later than StartAt");

        if (totalSeats <= 0)
            throw new ArgumentException("TotalSeats must be greater than zero", nameof(totalSeats));

        return new Event
        {
            Id = Guid.NewGuid(),
            Title = title,
            Description = description,
            StartAt = EnsureUtc(startAt),
            EndAt = EnsureUtc(endAt),
            TotalSeats = totalSeats,
            AvailableSeats = totalSeats
        };
    }

    /// <summary>
    /// Гарантирует, что DateTime имеет Kind=Utc.
    /// Если Kind уже Utc — возвращает как есть.
    /// Local конвертируется; Unspecified трактуется как UTC.
    /// </summary>
    private static DateTime EnsureUtc(DateTime dateTime)
    {
        return dateTime.Kind switch
        {
            DateTimeKind.Local => dateTime.ToUniversalTime(),
            DateTimeKind.Unspecified => DateTime.SpecifyKind(dateTime, DateTimeKind.Utc),
            _ => dateTime
        };
    }

    /// <summary>
    /// Резервирует места. Возвращает true, если успешно.
    /// </summary>
    public bool TryReserveSeats(int count = 1)
    {
        if (count <= 0 || AvailableSeats < count)
            return false;

        AvailableSeats -= count;
        return true;
    }

    /// <summary>
    /// Возвращает места в пул (при отмене/отклонении)
    /// </summary>
    public void ReleaseSeats(int count = 1)
    {
        if (count <= 0) return;

        AvailableSeats += count;
        if (AvailableSeats > TotalSeats)
            AvailableSeats = TotalSeats;
    }
    public void Update(
    string title,
    string? description,
    DateTime startAt,
    DateTime endAt)
    {
        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("Title is required", nameof(title));

        if (startAt >= endAt)
            throw new InvalidEventDatesException(
                "EndAt must be later than StartAt");

        Title = title;
        Description = description;
        StartAt = EnsureUtc(startAt);
        EndAt = EnsureUtc(endAt);
    }

}


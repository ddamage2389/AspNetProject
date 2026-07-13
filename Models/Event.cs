namespace AspNetProject.Models;

public class Event
{
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
            throw new ArgumentException("EndAt must be later than StartAt");

        if (totalSeats <= 0)
            throw new ArgumentException("TotalSeats must be greater than zero", nameof(totalSeats));

        return new Event
        {
            Id = Guid.NewGuid(),
            Title = title,
            Description = description,
            StartAt = startAt,
            EndAt = endAt,
            TotalSeats = totalSeats,
            AvailableSeats = totalSeats 
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
}


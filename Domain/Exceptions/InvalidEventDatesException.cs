namespace AspNetProject.Domain.Exceptions;

/// <summary>
/// Исключение, возникающее при нарушении бизнес-правила:
/// дата окончания должна быть строго позже даты начала.
/// </summary>
public class InvalidEventDatesException : Exception
{
    public InvalidEventDatesException(string message) : base(message) { }
}
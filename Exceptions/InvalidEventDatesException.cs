namespace AspNetProject.Exceptions;

public class InvalidEventDatesException : ArgumentException
{
    public InvalidEventDatesException(string message) : base(message) { }
}
namespace AspNetProject.Domain.Exceptions;

public class UnauthorizedCancellationException : Exception
{
    public UnauthorizedCancellationException(string message) : base(message) { }
}
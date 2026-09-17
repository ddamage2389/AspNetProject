using System.Net;
using System.Text.Json;
using AspNetProject.Domain.Exceptions;

namespace AspNetProject.Presentation.Middleware;

public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;

    public ExceptionHandlingMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception exception)
        {
            await HandleExceptionAsync(context, exception);
        }
    }

    private static Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        context.Response.ContentType = "application/json";

        var response = new ErrorResponse
        {
            Type = "https://tools.ietf.org/html/rfc7231#section-6.6.1",
            Title = "Внутренняя ошибка сервера",
            Detail = "Произошла непредвиденная ошибка.",
            Status = (int)HttpStatusCode.InternalServerError
        };

        switch (exception)
        {
            // 1. Специфичные доменные исключения (403 Forbidden)
            case UnauthorizedCancellationException unauthEx:
                response.Status = (int)HttpStatusCode.Forbidden;
                response.Title = "Доступ запрещен";
                response.Detail = unauthEx.Message;
                break;

            // 2. Лимиты и конфликты (409 Conflict)
            case BookingLimitExceededException limitEx:
                response.Status = (int)HttpStatusCode.Conflict;
                response.Title = "Лимит бронирований превышен";
                response.Detail = limitEx.Message;
                break;

            case NoAvailableSeatsException seatsEx:
                response.Status = (int)HttpStatusCode.Conflict;
                response.Title = "Нет свободных мест";
                response.Detail = seatsEx.Message;
                break;

            // 3. Ошибки валидации и бизнес-правил (400 Bad Request)
            case EventAlreadyStartedException startedEx:
                response.Status = (int)HttpStatusCode.BadRequest;
                response.Title = "Событие уже началось";
                response.Detail = startedEx.Message;
                break;

            case InvalidEventDatesException datesEx:
                response.Status = (int)HttpStatusCode.BadRequest;
                response.Title = "Некорректные даты события";
                response.Detail = datesEx.Message;
                break;

            case ArgumentException argEx:
                response.Status = (int)HttpStatusCode.BadRequest;
                response.Title = "Ошибка валидации";
                response.Detail = argEx.Message;
                break;

            // 4. Ресурс не найден (404 Not Found)
            case KeyNotFoundException:
                response.Status = (int)HttpStatusCode.NotFound;
                response.Title = "Ресурс не найден";
                response.Detail = exception.Message;
                break;

            // 5. Ошибки аутентификации (401 Unauthorized)
            case UnauthorizedAccessException:
                response.Status = (int)HttpStatusCode.Unauthorized;
                response.Title = "Не авторизован";
                response.Detail = "Неверный логин или пароль, либо отсутствуют права доступа.";
                break;
        }

        context.Response.StatusCode = response.Status;
        return context.Response.WriteAsync(JsonSerializer.Serialize(response));
    }

    private class ErrorResponse
    {
        public string Type { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public int Status { get; set; }
        public string Detail { get; set; } = string.Empty;
    }
}
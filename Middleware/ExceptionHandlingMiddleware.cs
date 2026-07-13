using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;

namespace AspNetProject.Middleware;

public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Произошла необработанная ошибка: {Message}", ex.Message);
            await HandleExceptionAsync(context, ex);
        }
    }

    private static Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        context.Response.ContentType = "application/json";

        var response = new ProblemDetails();

        response.Extensions["traceId"] = context.TraceIdentifier;
        switch (exception)
        {
            case ArgumentException argEx:
                context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
                response.Title = "Ошибка валидации";
                response.Detail = argEx.Message;
                response.Type = "https://tools.ietf.org/html/rfc7231#section-6.5.1";
                break;

            case AspNetProject.Exceptions.NoAvailableSeatsException:
                context.Response.StatusCode = (int)HttpStatusCode.Conflict; // 409
                response.Title = "Нет свободных мест";
                response.Detail = exception.Message;
                response.Type = "https://tools.ietf.org/html/rfc7231#section-6.5.8";
                break;

            case KeyNotFoundException:
                context.Response.StatusCode = (int)HttpStatusCode.NotFound;
                response.Title = "Ресурс не найден";
                response.Detail = exception.Message;
                response.Type = "https://tools.ietf.org/html/rfc7231#section-6.5.4";
                break;

            case UnauthorizedAccessException:
                context.Response.StatusCode = (int)HttpStatusCode.Unauthorized;
                response.Title = "Не авторизован";
                response.Detail = exception.Message;
                response.Type = "https://tools.ietf.org/html/rfc7235#section-3.1";
                break;

            default:
                context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
                response.Title = "Внутренняя ошибка сервера";
                response.Detail = "Произошла непредвиденная ошибка. Пожалуйста, попробуйте позже.";
                response.Type = "https://tools.ietf.org/html/rfc7231#section-6.6.1";
                break;
        }

        var result = JsonSerializer.Serialize(response);
        return context.Response.WriteAsync(result);
    }
}
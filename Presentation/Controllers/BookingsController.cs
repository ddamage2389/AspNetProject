using System.Security.Claims;
using AspNetProject.Application.Interfaces;
using AspNetProject.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AspNetProject.Presentation.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class BookingsController : ControllerBase
{
    private readonly IBookingService _bookingService;

    public BookingsController(IBookingService bookingService)
    {
        _bookingService = bookingService;
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetBookingById(Guid id)
    {
        var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var roleString = User.FindFirstValue(ClaimTypes.Role);

        if (!Guid.TryParse(userIdString, out var userId) || !Enum.TryParse<Role>(roleString, out var role))
        {
            return Unauthorized(new { message = "Некорректный токен" });
        }

        var booking = await _bookingService.GetBookingByIdAsync(id, userId, role);

        if (booking == null)
        {
            return NotFound(); // 404, если бронь не найдена или это чужая бронь
        }

        return Ok(booking); // 200 OK
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> CancelBooking(Guid id)
    {
        var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var roleString = User.FindFirstValue(ClaimTypes.Role);

        if (!Guid.TryParse(userIdString, out var userId) || !Enum.TryParse<Role>(roleString, out var role))
        {
            return Unauthorized(new { message = "Некорректный токен" });
        }

        try
        {
            await _bookingService.CancelBookingAsync(id, userId, role);
            return NoContent(); // 204 No Content при успешной отмене
        }
        catch (KeyNotFoundException)
        {
            return NotFound(); // 404, если бронь не найдена
        }
        // UnauthorizedCancellationException автоматически перехватится middleware и вернет 403 Forbidden
    }
}
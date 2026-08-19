using AspNetProject.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace AspNetProject.Controllers;

[Route("api/[controller]")]
[ApiController]
public class BookingsController : ControllerBase
{
    private readonly IBookingService _bookingService;

    public BookingsController(IBookingService bookingService)
    {
        _bookingService = bookingService;
    }

    /// <summary>
    /// Получение информации о брони по ID.
    /// GET /api/bookings/{id}
    /// </summary>
    [HttpGet("{id}")]
    public async Task<IActionResult> GetBookingById(Guid id)
    {
        var booking = await _bookingService.GetBookingByIdAsync(id);

        if (booking == null)
        {
            return NotFound();
        }

        return Ok(booking);
    }
}
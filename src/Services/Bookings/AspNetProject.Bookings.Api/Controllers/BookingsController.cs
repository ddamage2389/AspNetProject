using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using AspNetProject.Bookings.Application;
using AspNetProject.Bookings.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AspNetProject.Bookings.Api.Controllers;

public sealed record CreateBookingRequest(Guid EventId, [Range(1, int.MaxValue)] int Seats = 1);

[ApiController]
[Route("bookings")]
[Authorize]
public sealed class BookingsController(BookingService service, IConfiguration config) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<Booking>> Create(CreateBookingRequest request, CancellationToken ct)
    {
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)) return Unauthorized();
        var booking = await service.CreateAsync(request.EventId, userId, request.Seats,
            config.GetValue("BookingSettings:MaxActiveBookingsPerUser", 5), ct);
        return AcceptedAtAction(nameof(Get), new { id = booking.Id }, booking);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<Booking>> Get(Guid id, CancellationToken ct)
    {
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)) return Unauthorized();
        var booking = await service.GetAsync(id, userId, User.IsInRole("Admin"), ct);
        return booking is null ? NotFound() : Ok(booking);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Cancel(Guid id, CancellationToken ct)
    {
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)) return Unauthorized();
        await service.CancelAsync(id, userId, User.IsInRole("Admin"), ct);
        return NoContent();
    }
}

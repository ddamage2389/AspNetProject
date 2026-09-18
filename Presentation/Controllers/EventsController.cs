using AspNetProject.Application.Dtos;
using AspNetProject.Application.Interfaces;
using AspNetProject.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace AspNetProject.Presentation.Controllers;

[ApiController]
[Route("api/[controller]")]
public class EventsController : ControllerBase
{
    private readonly IEventService _eventService;
    private readonly IBookingService _bookingService;

    public EventsController(IEventService eventService, IBookingService bookingService)
    {
        _eventService = eventService;
        _bookingService = bookingService;
    }

    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<PaginatedResult<Event>>> GetAll(
        [FromQuery] string? title = null,
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10)
    {
        if (pageSize > 100) pageSize = 100;
        if (pageSize < 1) pageSize = 1;
        if (page < 1) page = 1;

        var result = await _eventService.GetAllAsync(title, from, to, page, pageSize);
        return Ok(result);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<Event>> GetById(Guid id)
    {
        var eventItem = await _eventService.GetByIdAsync(id);
        return eventItem == null ? NotFound() : Ok(eventItem);
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<Event>> Create([FromBody] CreateEventDto dto)
    {
        if (!dto.IsValidDateRange())
        {
            ModelState.AddModelError(nameof(dto.EndAt), "Поле EndAt должно быть позже поля StartAt");
            return BadRequest(ModelState);
        }

        var eventItem = Event.Create(dto.Title, dto.Description ?? string.Empty, dto.StartAt, dto.EndAt, dto.TotalSeats);


        var created = await _eventService.CreateAsync(eventItem);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpPut("{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateEventDto dto)
    {
        if (!dto.IsValidDateRange())
        {
            ModelState.AddModelError(nameof(dto.EndAt), "Поле EndAt должно быть позже поля StartAt");
            return BadRequest(ModelState);
        }

        var updated = await _eventService.UpdateAsync(id, dto);

        return updated == null ? NotFound() : Ok(updated);
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var deleted = await _eventService.DeleteAsync(id);
        return deleted ? NoContent() : NotFound();
    }

    [HttpPost("{id}/book")]
    [Authorize]
    public async Task<IActionResult> CreateBooking(Guid id, CancellationToken cancellationToken)
    {
        var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(userIdString, out var userId))
        {
            return Unauthorized(new { message = "Некорректный токен" });
        }

        try
        {
            var booking = await _bookingService.CreateBookingAsync(id, userId);
            return Accepted(booking); // 202 Accepted
        }
        catch (KeyNotFoundException)
        {
            return NotFound(); // 404
        }
    }
}
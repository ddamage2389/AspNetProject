using AspNetProject.Dtos;
using AspNetProject.Models;
using AspNetProject.Services;
using Microsoft.AspNetCore.Mvc;

namespace AspNetProject.Controllers;

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
    public async Task<ActionResult<Event>> Create([FromBody] CreateEventDto dto)
    {
        if (!dto.IsValidDateRange())
        {
            ModelState.AddModelError(nameof(dto.EndAt),
                "Поле EndAt должно быть позже поля StartAt");
            return BadRequest(ModelState);
        }

        var eventItem = new Event
        {
            Title = dto.Title,
            Description = dto.Description,
            StartAt = dto.StartAt,
            EndAt = dto.EndAt
        };

        var created = await _eventService.CreateAsync(eventItem);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateEventDto dto)
    {
        if (!dto.IsValidDateRange())
        {
            ModelState.AddModelError(nameof(dto.EndAt),
                "Поле EndAt должно быть позже поля StartAt");
            return BadRequest(ModelState);
        }

        var eventToUpdate = new Event
        {
            Title = dto.Title,
            Description = dto.Description,
            StartAt = dto.StartAt,
            EndAt = dto.EndAt
        };

        var updated = await _eventService.UpdateAsync(id, eventToUpdate);
        return updated == null ? NotFound() : Ok(updated);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var deleted = await _eventService.DeleteAsync(id);
        return deleted ? NoContent() : NotFound();
    }

    [HttpGet("test-error")]
    public IActionResult TestError()
    {
        throw new Exception("Это тестовое исключение для проверки middleware");
    }

    [HttpPost("{id}/book")]
    public async Task<IActionResult> CreateBooking(Guid id)
    {
        try
        {
            var booking = await _bookingService.CreateBookingAsync(id);

            // Возвращаем 202 Accepted с заголовком Location
            return AcceptedAtAction(
                nameof(BookingsController.GetBookingById),
                "Bookings",
                new { id = booking.Id },
                booking);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }
}




using AspNetProject.Models;
using AspNetProject.Dtos;
using AspNetProject.Services;
using Microsoft.AspNetCore.Mvc;

namespace AspNetProject.Controllers;

[ApiController]
[Route("api/[controller]")]
public class EventsController : ControllerBase
{
    private readonly IEventService _eventService;

    public EventsController(IEventService eventService)
    {
        _eventService = eventService;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<Event>>> GetAll()
    {
        var events = await _eventService.GetAllAsync();
        return Ok(events);
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
}
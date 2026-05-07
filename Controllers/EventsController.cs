
using AspNetApi.Models;
using EventManager.Dtos;
using EventManager.Services;
using Microsoft.AspNetCore.Mvc;

namespace EventManager.Controllers;

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

        if (eventItem is null)
            return NotFound(new { message = $"Событие с id {id} не найдено" });

        return Ok(eventItem);
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

        var existing = await _eventService.GetByIdAsync(id);
        if (existing is null)
            return NotFound(new { message = $"Событие с id {id} не найдено" });

        existing.Title = dto.Title;
        existing.Description = dto.Description;
        existing.StartAt = dto.StartAt;
        existing.EndAt = dto.EndAt;

        await _eventService.UpdateAsync(id, existing);

        return Ok(existing);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var deleted = await _eventService.DeleteAsync(id);

        if (!deleted)
            return NotFound(new { message = $"Событие с id {id} не найдено" });

        return NoContent();
    }
}
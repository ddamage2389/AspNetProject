using AspNetProject.Application.Dtos;
using AspNetProject.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace AspNetProject.Presentation.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IUserService _userService;

    public AuthController(IUserService userService)
    {
        _userService = userService;
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterUserDto dto, CancellationToken cancellationToken)
    {
        try
        {
            await _userService.RegisterAsync(dto, cancellationToken);
            return NoContent(); // 204 No Content при успехе
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message }); // 400 при ошибке
        }
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginUserDto dto, CancellationToken cancellationToken)
    {
        try
        {
            var token = await _userService.LoginAsync(dto, cancellationToken);
            return Ok(new { token }); // 200 OK с токеном
        }
        catch (UnauthorizedAccessException)
        {
            return Unauthorized(new { message = "Неверный логин или пароль." });
        }
    }
}
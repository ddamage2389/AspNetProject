using System.ComponentModel.DataAnnotations;

namespace AspNetProject.Application.Dtos;

public class RegisterUserDto
{
    [Required]
    [StringLength(50, MinimumLength = 3)]
    public string Login { get; set; } = string.Empty;

    [Required]
    [StringLength(100, MinimumLength = 6)]
    public string Password { get; set; } = string.Empty;

    public string? Role { get; set; }
}
using System.ComponentModel.DataAnnotations;

namespace AspNetProject.Users.Application.Dtos;

public class LoginUserDto
{
    [Required]
    public string Login { get; set; } = string.Empty;

    [Required]
    public string Password { get; set; } = string.Empty;
}
using System.ComponentModel.DataAnnotations;

namespace AspNetProject.Users.Application.Dtos;

public class RegisterUserDto
{
    [Required]
    [StringLength(50, MinimumLength = 3)]
    public string Login { get; set; } = string.Empty;

    [Required]
    [StringLength(100, MinimumLength = 6)]
    public string Password { get; set; } = string.Empty;

}
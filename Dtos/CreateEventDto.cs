
using System.ComponentModel.DataAnnotations;

namespace AspNetProject.Dtos;

public class CreateEventDto
{
    [Required(ErrorMessage = "Поле Title обязательно")]
    [StringLength(200, MinimumLength = 1, ErrorMessage = "Title должен содержать от 1 до 200 символов")]
    public string Title { get; set; } = string.Empty;

    [StringLength(2000, ErrorMessage = "Description не должен превышать 2000 символов")]
    public string? Description { get; set; }

    [Required(ErrorMessage = "Поле StartAt обязательно")]
    public DateTime StartAt { get; set; }

    [Required(ErrorMessage = "Поле EndAt обязательно")]
    public DateTime EndAt { get; set; }

    /// <summary>
    /// Дополнительная валидация: EndAt должен быть позже StartAt
    /// </summary>
    public bool IsValidDateRange() => EndAt > StartAt;
}
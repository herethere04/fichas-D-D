using System.ComponentModel.DataAnnotations;

namespace DnDSheetApi.Application.DTOs;

public class CreateSheetRequest
{
    [Required, MaxLength(100)]
    public string CharacterName { get; set; } = string.Empty;

    [Required, MinLength(3), MaxLength(50)]
    public string EditPassword { get; set; } = string.Empty;
}

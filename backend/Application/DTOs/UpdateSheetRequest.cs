using System.ComponentModel.DataAnnotations;

namespace DnDSheetApi.Application.DTOs;

public class UpdateSheetRequest
{
    [Required]
    public string EditPassword { get; set; } = string.Empty;

    [Required]
    public string SheetData { get; set; } = "{}";
}

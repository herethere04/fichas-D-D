using System.ComponentModel.DataAnnotations;

namespace DnDSheetApi.Application.DTOs;

public class VerifyPasswordRequest
{
    [Required]
    public string EditPassword { get; set; } = string.Empty;
}

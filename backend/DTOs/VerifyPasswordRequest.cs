using System.ComponentModel.DataAnnotations;

namespace DnDSheetApi.DTOs;

public class VerifyPasswordRequest
{
    [Required]
    public string EditPassword { get; set; } = string.Empty;
}

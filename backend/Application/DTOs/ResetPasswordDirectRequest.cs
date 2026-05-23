using System.ComponentModel.DataAnnotations;

namespace DnDSheetApi.Application.DTOs;

public class ResetPasswordDirectRequest
{
    [Required]
    [MinLength(3)]
    public string NewPassword { get; set; } = string.Empty;
}

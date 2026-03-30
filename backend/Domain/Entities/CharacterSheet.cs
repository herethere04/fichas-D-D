using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DnDSheetApi.Domain.Entities;

public class CharacterSheet
{
    [Key]
    public int Id { get; set; }

    [Required, MaxLength(100)]
    public string CharacterName { get; set; } = string.Empty;

    [Required]
    public string EditPasswordHash { get; set; } = string.Empty;

    [Column(TypeName = "jsonb")]
    public string SheetData { get; set; } = "{}";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

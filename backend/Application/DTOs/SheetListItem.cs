namespace DnDSheetApi.Application.DTOs;

public class SheetListItem
{
    public int Id { get; set; }
    public string CharacterName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

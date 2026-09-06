namespace DnDSheetApi.Domain.Models;

// Read models keep the large JSON and password hash out of queries that do not need them.
public record SheetSummary(int Id, string CharacterName, DateTime CreatedAt, DateTime UpdatedAt);

public record SheetDetails(int Id, string CharacterName, string SheetData, DateTime CreatedAt, DateTime UpdatedAt);

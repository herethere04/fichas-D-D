using DnDSheetApi.Domain.Entities;
using DnDSheetApi.Domain.Models;

namespace DnDSheetApi.Application.Interfaces;

public interface ISheetService
{
    Task<IEnumerable<SheetSummary>> GetAllSheetsAsync();
    Task<SheetDetails?> GetSheetByIdAsync(int id);
    Task<CharacterSheet> CreateSheetAsync(string characterName, string editPassword);
    Task<bool> UpdateSheetAsync(int id, string editPassword, string sheetData);
    Task<bool> DeleteSheetAsync(int id, string editPassword);
    Task<bool> VerifyPasswordAsync(int id, string editPassword);
    Task<bool> ResetPasswordDirectAsync(int id, string newPassword);
}
